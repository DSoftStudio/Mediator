// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry;
using DSoft.Sample.OpenTelemetry.Application.Commands;
using DSoft.Sample.OpenTelemetry.Application.Notifications;
using DSoft.Sample.OpenTelemetry.Application.Queries;
using DSoft.Sample.OpenTelemetry.Application.Streams;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ── Mediator + OpenTelemetry instrumentation ─────────────────────────
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddMediatorInstrumentation(options =>
    {
        // Example: suppress instrumentation for health checks
        options.Filter = type => !type.Name.StartsWith("HealthCheck");
    })
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();

// ── OpenTelemetry SDK: export to console ─────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddMediatorInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMediatorInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

// ── Endpoints ────────────────────────────────────────────────────────

// POST /orders — command → creates a span "CreateOrderCommand command"
app.MapPost("/orders", async (CreateOrderRequest request, IMediator mediator) =>
{
    var orderId = await mediator.Send(new CreateOrderCommand(request.Product, request.Quantity));

    // Publish notification → creates parent "OrderCreatedNotification publish"
    //                        with child spans per handler
    await mediator.Publish(new OrderCreatedNotification(orderId, request.Product));

    return Results.Created($"/orders/{orderId}", new { id = orderId });
});

// GET /orders — query → creates a span "GetOrdersQuery query"
app.MapGet("/orders", async (IMediator mediator) =>
{
    var orders = await mediator.Send(new GetOrdersQuery());
    return Results.Ok(orders);
});

// GET /orders/stream — stream → creates a span "StreamOrderUpdates stream"
app.MapGet("/orders/stream", async (IMediator mediator, HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/event-stream";
    await foreach (var update in mediator.CreateStream(new StreamOrderUpdates()))
    {
        await ctx.Response.WriteAsync($"data: {update}\n\n");
        await ctx.Response.Body.FlushAsync();
    }
});

app.Run();

record CreateOrderRequest(string Product, int Quantity);
