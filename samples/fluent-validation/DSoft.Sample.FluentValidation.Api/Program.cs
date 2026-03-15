// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.FluentValidation;
using DSoft.Sample.FluentValidation.Application.Commands;
using DSoft.Sample.FluentValidation.Application.Queries;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// ── Mediator + FluentValidation ──────────────────────────────────────
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers();

// Register validators
builder.Services.AddTransient<IValidator<CreateProductCommand>, CreateProductValidator>();

// Register validation behavior + precompile
builder.Services
    .AddMediatorFluentValidation()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();

var app = builder.Build();

// ── Global error handler: MediatorValidationException → 400 ──────────
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (MediatorValidationException ex)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            title = "Validation Failed",
            status = 400,
            errors = ex.ErrorsByProperty
        });
    }
});

// ── Endpoints ────────────────────────────────────────────────────────

// POST /products — with validation
app.MapPost("/products", async (CreateProductRequest request, IMediator mediator) =>
{
    var id = await mediator.Send(new CreateProductCommand(request.Name, request.Price));
    return Results.Created($"/products/{id}", new { id });
});

// GET /products/{id} — no validator, passes through
app.MapGet("/products/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var product = await mediator.Send(new GetProductQuery(id));
    return Results.Ok(product);
});

app.Run();

record CreateProductRequest(string Name, decimal Price);
