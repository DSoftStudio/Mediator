// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache;
using DSoft.Sample.HybridCache.Application.Commands;
using DSoft.Sample.HybridCache.Application.Queries;

var builder = WebApplication.CreateBuilder(args);

// ── Mediator + HybridCache ───────────────────────────────────────────
builder.Services
    .AddMediator()
    .RegisterMediatorHandlers();

// HybridCache (L1 in-memory by default; add Redis for L2)
builder.Services.AddHybridCache();

// Register caching behavior + precompile
builder.Services
    .AddMediatorHybridCache()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();

var app = builder.Build();

// ── Endpoints ────────────────────────────────────────────────────────

// GET /products/{id} — cached query (handler called once per key within TTL)
app.MapGet("/products/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var product = await mediator.Send(new GetProductQuery(id));
    return Results.Ok(product);
});

// POST /products — command (not cached)
app.MapPost("/products", async (CreateProductRequest request, IMediator mediator) =>
{
    var id = await mediator.Send(new CreateProductCommand(request.Name, request.Price));
    return Results.Created($"/products/{id}", new { id });
});

app.Run();

record CreateProductRequest(string Name, decimal Price);
