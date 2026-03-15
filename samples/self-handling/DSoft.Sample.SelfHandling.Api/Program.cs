// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoft.Sample.SelfHandling.Application.Commands;
using DSoft.Sample.SelfHandling.Application.Queries;
using DSoft.Sample.SelfHandling.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Register application services used by self-handlers
builder.Services.AddSingleton<GreetingService>();

// Register mediator + handlers + precompile pipelines
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines();

var app = builder.Build();

// GET /greet?name=World — self-handling query with DI service injection
app.MapGet("/greet", async (string name, IMediator mediator) =>
{
    var result = await mediator.Send(new GreetQuery(name));
    return Results.Ok(result);
});

// POST /multiply — sync self-handling command
app.MapPost("/multiply", async (MultiplyRequest request, IMediator mediator) =>
{
    var result = await mediator.Send(new MultiplyCommand(request.A, request.B));
    return Results.Ok(result);
});

// GET /ping?value=21 — async self-handling query
app.MapGet("/ping", async (int value, IMediator mediator) =>
{
    var result = await mediator.Send(new DelayedPingQuery(value));
    return Results.Ok(result);
});

// POST /log — void self-handling command (returns Unit)
app.MapPost("/log", async (LogRequest request, IMediator mediator) =>
{
    await mediator.Send(new LogMessageCommand(request.Message));
    return Results.Ok("Logged");
});

app.Run();

// Request DTOs
record MultiplyRequest(int A, int B);
record LogRequest(string Message);
