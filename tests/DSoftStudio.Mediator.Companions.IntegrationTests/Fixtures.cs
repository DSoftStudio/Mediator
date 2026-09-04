// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache;
using FluentValidation;

namespace DSoftStudio.Mediator.Companions.IntegrationTests;

/// <summary>Counts handler invocations across a whole provider, so a cache hit is observable.</summary>
public sealed class CallLog
{
    private int _handlerCalls;
    private int _validatorCalls;
    private readonly List<string> _tenantsSeen = [];

    public int HandlerCalls => Volatile.Read(ref _handlerCalls);
    public int ValidatorCalls => Volatile.Read(ref _validatorCalls);

    /// <summary>What each handler invocation read out of ambient state, in invocation order.</summary>
    public IReadOnlyList<string> TenantsSeen
    {
        get { lock (_tenantsSeen) return [.. _tenantsSeen]; }
    }

    public void Handler() => Interlocked.Increment(ref _handlerCalls);
    public void Validator() => Interlocked.Increment(ref _validatorCalls);

    public void SawTenant(string? tenant)
    {
        lock (_tenantsSeen)
            _tenantsSeen.Add(tenant ?? "<none>");
    }
}

/// <summary>
/// The ambient value a handler is supposed to inherit from whoever called Send. An AsyncLocal is the
/// cheapest stand-in for IHttpContextAccessor or Activity.Current — it flows the same way, through the
/// ExecutionContext, and breaks the same way when that context is not restored.
/// </summary>
public static class AmbientTenant
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? Value
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}

/// <summary>A cached query whose handler blocks until released, so a stampede can be arranged.</summary>
public sealed record GetReport(string Key, TimeSpan Ttl) : IQuery<string>, ICachedRequest
{
    public string CacheKey => $"{nameof(GetReport)}:{Key}";
    public TimeSpan Duration => Ttl;
}

public sealed class GetReportHandler(CallLog log, ReportGate gate) : IRequestHandler<GetReport, string>
{
    public async ValueTask<string> Handle(GetReport request, CancellationToken cancellationToken)
    {
        log.Handler();
        log.SawTenant(AmbientTenant.Value);

        if (gate.Hold is not null)
            await gate.Hold.Task.WaitAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (gate.Throw)
            throw new InvalidOperationException("report unavailable");

        return $"report:{request.Key}:{log.HandlerCalls}";
    }
}

/// <summary>Lets a test hold the handler open, or make it fail, without a new fixture per case.</summary>
public sealed class ReportGate
{
    public TaskCompletionSource? Hold;
    public bool Throw;
}

/// <summary>A request that is BOTH cached and validated — the interaction under test.</summary>
public sealed record GetAccount(string AccountId) : IQuery<string>, ICachedRequest
{
    public string CacheKey => $"{nameof(GetAccount)}:{AccountId}";
    public TimeSpan Duration => TimeSpan.FromMinutes(5);
}

public sealed class GetAccountHandler(CallLog log) : IRequestHandler<GetAccount, string>
{
    public ValueTask<string> Handle(GetAccount request, CancellationToken cancellationToken)
    {
        log.Handler();
        return new($"account:{request.AccountId}");
    }
}

public sealed class GetAccountValidator : AbstractValidator<GetAccount>
{
    public GetAccountValidator(CallLog log)
    {
        RuleFor(x => x.AccountId).NotEmpty().WithMessage("Account id is required.");

        // Counted through a rule rather than the constructor: the behavior is Scoped and the chain is
        // cached, so constructing the validator once would say nothing about how often it RAN.
        RuleFor(x => x).Custom((_, _) => log.Validator());
    }
}

/// <summary>
/// A SECOND, different validator for the same request. Two validators with DIFFERENT messages is the
/// shape the shared-context defect needed: with one validator, or with two identical ones, a duplicate
/// is indistinguishable from a legitimate repeat.
/// </summary>
public sealed class GetAccountFormatValidator : AbstractValidator<GetAccount>
{
    public GetAccountFormatValidator()
        => RuleFor(x => x.AccountId).MinimumLength(4).WithMessage("Account id is too short.");
}

/// <summary>A validator that actually awaits, and observes the token it was handed.</summary>
public sealed record SlowCommand(string Value) : ICommand<string>;

public sealed class SlowCommandHandler : IRequestHandler<SlowCommand, string>
{
    public ValueTask<string> Handle(SlowCommand request, CancellationToken cancellationToken)
        => new(request.Value);
}

public sealed class SlowCommandValidator : AbstractValidator<SlowCommand>
{
    public SlowCommandValidator(TokenProbe probe)
    {
        RuleFor(x => x.Value).MustAsync(async (_, ct) =>
        {
            probe.Observed = ct;
            await Task.Yield();
            return true;
        });
    }
}

/// <summary>Captures the CancellationToken a validator was handed.</summary>
public sealed class TokenProbe
{
    public CancellationToken Observed;
}

/// <summary>Scoped, so a per-scope resolution is observable from inside a validator.</summary>
public sealed record ScopedCommand(string Value) : ICommand<string>;

public sealed class ScopedCommandHandler : IRequestHandler<ScopedCommand, string>
{
    public ValueTask<string> Handle(ScopedCommand request, CancellationToken cancellationToken)
        => new(request.Value);
}

public sealed class ScopeMarker
{
    public readonly Guid Id = Guid.NewGuid();
}

public sealed class ScopedCommandValidator : AbstractValidator<ScopedCommand>
{
    public ScopedCommandValidator(ScopeMarker marker, ScopeObservations seen)
    {
        // Recorded from a RULE, not from an override of Validate: the behavior calls ValidateAsync,
        // which is a separate virtual on AbstractValidator and does not route through the synchronous
        // one — an override there is simply never invoked, and the test would pass on an empty list.
        RuleFor(x => x).Custom((_, _) => seen.Add(marker.Id));
    }
}

public sealed class ScopeObservations
{
    private readonly List<Guid> _ids = [];

    public void Add(Guid id)
    {
        lock (_ids)
            _ids.Add(id);
    }

    public IReadOnlyList<Guid> Ids
    {
        get { lock (_ids) return [.. _ids]; }
    }
}
