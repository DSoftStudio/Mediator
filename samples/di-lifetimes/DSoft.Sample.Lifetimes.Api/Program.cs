// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoft.Sample.Lifetimes.Application.Behaviors;
using DSoft.Sample.Lifetimes.Application.Queries;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════
// 1. Register mediator + auto-discovered handlers
// ═══════════════════════════════════════════════════════════════════
//
// RegisterMediatorHandlers() auto-discovers handlers and registers them:
//   - Stateless (no constructor params) → Singleton (zero allocation per call)
//   - With DI dependencies              → Transient (safe default)
// To override the lifetime for specific handlers, re-register them AFTER
// this call — the last registration wins in Microsoft.Extensions.DI.

builder.Services
    .AddMediator()
    .RegisterMediatorHandlers();

// ═══════════════════════════════════════════════════════════════════
// 2. Override handler lifetimes
// ═══════════════════════════════════════════════════════════════════
//
// All three handlers above are stateless (no constructor params), so the
// generator auto-registered them as Singleton. Override to demonstrate
// different lifetimes. The DI container uses the LAST registration.

// Transient: new instance on every resolution (override auto-Singleton)
builder.Services.AddTransient<
    IRequestHandler<TransientQuery, LifetimeInfo>,
    TransientQueryHandler>();

// Scoped: same instance within one HTTP request / DI scope (override auto-Singleton)
builder.Services.AddScoped<
    IRequestHandler<ScopedQuery, LifetimeInfo>,
    ScopedQueryHandler>();

// SingletonQueryHandler stays Singleton (matches the auto-registered default)

// ═══════════════════════════════════════════════════════════════════
// 3. Register pipeline behaviors with explicit lifetimes
// ═══════════════════════════════════════════════════════════════════
//
// Behaviors can have any lifetime. Choose based on what state they hold:
//   - Transient: stateless (logging, validation)
//   - Scoped:    per-request state (counters, correlation IDs, UnitOfWork)
//   - Singleton: global state (metrics, rate limiting) — must be thread-safe!

// Singleton behavior: counts total requests across all HTTP requests
builder.Services.AddSingleton(
    typeof(IPipelineBehavior<,>),
    typeof(SingletonMetricsBehavior<,>));

// Scoped behavior: counts requests within the current HTTP request
builder.Services.AddScoped(
    typeof(IPipelineBehavior<,>),
    typeof(ScopedCounterBehavior<,>));

// Precompile after all handlers, behaviors, and lifetime overrides are registered
builder.Services.PrecompilePipelines();

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════
// Endpoints — call each multiple times to observe lifetime behavior
// ═══════════════════════════════════════════════════════════════════

// GET /transient — InstanceId changes on EVERY request
app.MapGet("/transient", async (IMediator mediator) =>
    Results.Ok(await mediator.Send(new TransientQuery())));

// GET /scoped — InstanceId same within one request, different across requests
app.MapGet("/scoped", async (IMediator mediator) =>
    Results.Ok(await mediator.Send(new ScopedQuery())));

// GET /singleton — InstanceId NEVER changes (same for all requests)
app.MapGet("/singleton", async (IMediator mediator) =>
    Results.Ok(await mediator.Send(new SingletonQuery())));

// GET /compare — sends all three in one request to compare side by side
app.MapGet("/compare", async (IMediator mediator) =>
{
    var transient = await mediator.Send(new TransientQuery());
    var scoped = await mediator.Send(new ScopedQuery());
    var singleton = await mediator.Send(new SingletonQuery());

    return Results.Ok(new { transient, scoped, singleton });
});

app.Run();
