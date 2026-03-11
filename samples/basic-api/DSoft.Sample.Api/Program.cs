// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoft.Sample.Application.Commands;
using DSoft.Sample.Application.Queries;

var builder = WebApplication.CreateBuilder(args);

// Register mediator + handlers + precompile pipelines
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers()
    .PrecompilePipelines();

var app = builder.Build();

// GET /hello — query example
app.MapGet("/hello", async (IMediator mediator) =>
{
    var result = await mediator.Send(new HelloWorldQuery());
    return Results.Ok(result);
});

// POST /multiply — command example
app.MapPost("/multiply", async (MultiplyRequest request, IMediator mediator) =>
{
    var result = await mediator.Send(new MultiplyCommand(request.A, request.B));
    return Results.Ok(result);
});

app.Run();

// Request DTO for the /multiply endpoint
record MultiplyRequest(int A, int B);
