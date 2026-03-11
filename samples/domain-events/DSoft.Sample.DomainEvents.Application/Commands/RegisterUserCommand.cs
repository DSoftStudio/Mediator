// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.DomainEvents.Application.Commands;

public record RegisterUserCommand(string Email) : ICommand<Guid>;

public sealed class RegisterUserCommandHandler(IMediator mediator)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async ValueTask<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Simulate user creation
        var userId = Guid.NewGuid();

        // Publish domain event — all handlers react independently
        await mediator.Publish(
            new Events.UserRegisteredEvent(userId, request.Email),
            cancellationToken);

        return userId;
    }
}
