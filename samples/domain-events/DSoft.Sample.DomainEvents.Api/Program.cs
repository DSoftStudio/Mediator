// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoft.Sample.DomainEvents.Application.Commands;

var builder = WebApplication.CreateBuilder(args);

// Register mediator + handlers
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines()
    .PrecompileNotifications();

var app = builder.Build();

// POST /users — registers a user and publishes a domain event.
// Three independent handlers react:
//   1. SendWelcomeEmailHandler  → sends welcome email
//   2. AuditLogHandler          → writes audit log
//   3. ProvisionUserDefaultsHandler → creates default settings
app.MapPost("/users", async (RegisterUserRequest request, IMediator mediator) =>
{
    var userId = await mediator.Send(
        new RegisterUserCommand(request.Email));

    return Results.Created($"/users/{userId}", new { UserId = userId });
});

app.Run();

record RegisterUserRequest(string Email);
