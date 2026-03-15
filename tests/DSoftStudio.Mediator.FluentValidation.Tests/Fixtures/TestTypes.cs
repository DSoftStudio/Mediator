// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using FluentValidation;

namespace DSoftStudio.Mediator.FluentValidation.Tests.Fixtures;

// ── Commands ──────────────────────────────────────────────────────────

public record CreateUser(string Name, string Email) : ICommand<Guid>;

public sealed class CreateUserHandler : IRequestHandler<CreateUser, Guid>
{
    public ValueTask<Guid> Handle(CreateUser request, CancellationToken cancellationToken)
        => new(Guid.NewGuid());
}

public sealed class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required.");
    }
}

// ── Queries ───────────────────────────────────────────────────────────

public record GetUser(Guid Id) : IQuery<string>;

public sealed class GetUserHandler : IRequestHandler<GetUser, string>
{
    public ValueTask<string> Handle(GetUser request, CancellationToken cancellationToken)
        => new($"user:{request.Id}");
}

// ── Request with no validators ────────────────────────────────────────

public record Ping(int Value) : IRequest<int>;

public sealed class PingHandler : IRequestHandler<Ping, int>
{
    public ValueTask<int> Handle(Ping request, CancellationToken cancellationToken)
        => new(request.Value * 2);
}

// ── Request with multiple validators ──────────────────────────────────

public record TransferMoney(string From, string To, decimal Amount) : ICommand<string>;

public sealed class TransferMoneyHandler : IRequestHandler<TransferMoney, string>
{
    public ValueTask<string> Handle(TransferMoney request, CancellationToken cancellationToken)
        => new($"transferred:{request.Amount}");
}

public sealed class TransferMoneyAccountValidator : AbstractValidator<TransferMoney>
{
    public TransferMoneyAccountValidator()
    {
        RuleFor(x => x.From).NotEmpty().WithMessage("From account is required.");
        RuleFor(x => x.To).NotEmpty().WithMessage("To account is required.");
    }
}

public sealed class TransferMoneyAmountValidator : AbstractValidator<TransferMoney>
{
    public TransferMoneyAmountValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be positive.");
    }
}

// ── Validator with DI dependency ──────────────────────────────────────

public interface IBlockedAccountService
{
    bool IsBlocked(string account);
}

public sealed class BlockedAccountService : IBlockedAccountService
{
    private readonly HashSet<string> _blocked = ["BLOCKED-001"];
    public bool IsBlocked(string account) => _blocked.Contains(account);
}

public sealed class TransferMoneyBlockedValidator : AbstractValidator<TransferMoney>
{
    public TransferMoneyBlockedValidator(IBlockedAccountService blockedService)
    {
        RuleFor(x => x.From)
            .Must(account => !blockedService.IsBlocked(account))
            .WithMessage("Source account is blocked.");
    }
}
