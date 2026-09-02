// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Validation;

public sealed record OrphanPing : IRequest<int>;

public sealed class OrphanPingHandler : IRequestHandler<OrphanPing, int>
{
    public ValueTask<int> Handle(OrphanPing request, CancellationToken ct) => new(1);
}

public sealed class OrphanBehavior : IPipelineBehavior<OrphanPing, int>
{
    public ValueTask<int> Handle(OrphanPing request, IRequestHandler<OrphanPing, int> next, CancellationToken ct)
        => next.Handle(request, ct);
}

/// <summary>
/// <c>PrecompilePipelines()</c> decides, per request type, whether a pipeline chain is built at all.
/// A component registered after that scan never runs, and until now it did so in complete silence —
/// no exception, no diagnostic, a dispatch that looks perfectly healthy.
/// <para>
/// <c>ValidateMediatorHandlers()</c> is the only place this is visible, because it inspects the built
/// container rather than one syntax tree: it catches the case even when the late registration lives in
/// another method, another file or another assembly (an <c>AddMediatorFluentValidation()</c> call
/// inside an extension method, say).
/// </para>
/// </summary>
public class OrphanedComponentValidationTests
{
    private const string OrphanMessage = "will NEVER run";

    [Fact]
    public void BehaviorRegisteredAfterTheScan_IsReported()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.PrecompilePipelines();

        // Too late: the scan already decided this pair needs no chain. DSOFT010 reports exactly this,
        // and reporting it here is the point of the test — the validator has to catch it too, for the
        // cases the analyzer cannot see (another method, another assembly).
#pragma warning disable DSOFT010
        services.AddTransient<IPipelineBehavior<OrphanPing, int>, OrphanBehavior>();
#pragma warning restore DSOFT010

        using var provider = services.BuildServiceProvider();

        var error = Should.Throw<AggregateException>(() => provider.ValidateMediatorHandlers());

        error.InnerExceptions.ShouldContain(e =>
            e.Message.Contains(nameof(OrphanPing)) && e.Message.Contains(OrphanMessage));
    }

    [Fact]
    public void BehaviorRegisteredBeforeTheScan_IsNotReported()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();

        // In time: the scan sees it and builds the chain.
        services.AddTransient<IPipelineBehavior<OrphanPing, int>, OrphanBehavior>();
        services.PrecompilePipelines();

        using var provider = services.BuildServiceProvider();

        // Non-vacuous: the chain really was built, which is the condition the report keys on.
        provider.GetService<PipelineChainHandler<OrphanPing, int>>().ShouldNotBeNull();

        // Other handlers in this assembly have dependencies this minimal container does not register,
        // so validation still throws — but never about OrphanPing.
        var error = Should.Throw<AggregateException>(() => provider.ValidateMediatorHandlers());

        error.InnerExceptions.ShouldNotContain(e =>
            e.Message.Contains(nameof(OrphanPing)) && e.Message.Contains(OrphanMessage));
    }
}
