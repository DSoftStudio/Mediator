// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoft.Sample.Pipeline.Application.Behaviors;
using DSoft.Sample.Pipeline.Application.Commands;

var builder = WebApplication.CreateBuilder(args);

// Register mediator + handlers
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers();

// Register pipeline behaviors (execution order = registration order)
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Precompile after all pipeline components are registered
builder.Services.PrecompilePipelines();

var app = builder.Build();

// POST /orders — creates an order through the pipeline:
//   LoggingBehavior → ValidationBehavior → CreateOrderCommandHandler
app.MapPost("/orders", async (CreateOrderRequest request, IMediator mediator) =>
{
    var orderId = await mediator.Send(
        new CreateOrderCommand(request.ProductName, request.Quantity));

    return Results.Created($"/orders/{orderId}", new { OrderId = orderId });
});

app.Run();

record CreateOrderRequest(string ProductName, int Quantity);
