// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoft.Sample.Processors.Application.Commands;
using DSoft.Sample.Processors.Application.Processors;
using DSoft.Sample.Processors.Application.Queries;

var builder = WebApplication.CreateBuilder(args);

// Register mediator + handlers
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers();

// Register pre-processors (execution order = registration order, before handler)
builder.Services.AddTransient(typeof(IRequestPreProcessor<>), typeof(LoggingPreProcessor<>));
builder.Services.AddTransient(typeof(IRequestPreProcessor<>), typeof(ValidationPreProcessor<>));

// Register post-processor (runs after handler, only on success)
builder.Services.AddTransient(typeof(IRequestPostProcessor<,>), typeof(AuditPostProcessor<,>));

// Precompile after all pipeline components are registered
builder.Services.PrecompilePipelines();

var app = builder.Build();

// POST /orders — pipeline: LoggingPre → ValidationPre → Handler → AuditPost
app.MapPost("/orders", async (PlaceOrderRequest request, IMediator mediator) =>
{
    var orderId = await mediator.Send(
        new PlaceOrderCommand(request.ProductName, request.Quantity));

    return Results.Created($"/orders/{orderId}", new { OrderId = orderId });
});

// GET /orders/{id} — pipeline: LoggingPre → Handler → AuditPost (validation skipped for queries)
app.MapGet("/orders/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var order = await mediator.Send(new GetOrderQuery(id));
    return Results.Ok(order);
});

app.Run();

record PlaceOrderRequest(string ProductName, int Quantity);
