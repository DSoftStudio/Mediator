// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// ============================================================================
// Cross-Project Mocking Sample — Host (Composition Root)
// ============================================================================
//
// This project references DSoftStudio.Mediator (with source generators).
// The generators discover handlers from Host.Application via
// ReferencedAssemblyScanner Phase 2 (type-based fallback) and generate:
//
//   1. Typed extensions:  sender.Send(new CreateOrderCommand(...))
//   2. DI registration:   services.RegisterMediatorHandlers()
//   3. Interceptors:      direct pipeline dispatch (bypasses virtual calls)
//
// Host.Application only references Abstractions — no generators run there.
// This is the recommended architecture for testability.
// ============================================================================

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Host.Application.Behaviors;
using Host.Application.Queries;
using Host.Application.Services;
using Host.Application.Commands;

var services = new ServiceCollection();

// Register mediator + handlers
services.AddMediator()
    .RegisterMediatorHandlers();

// Register pipeline behaviors (execution order = registration order)
services.AddSingleton<Action<string>>(Console.WriteLine);
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

services.AddTransient<OrderService>();

// Precompile after all pipeline components are registered
services.PrecompilePipelines();

using var provider = services.BuildServiceProvider();

var orderService = provider.GetRequiredService<OrderService>();

// Place an order — routes through the compiled pipeline
var orderId = await orderService.PlaceOrderAsync("Widget", 5);
Console.WriteLine($"Created order: {orderId}");

// Query the order
var summary = await orderService.GetOrderSummaryAsync(orderId);
Console.WriteLine(summary);

// Look up a user — nullable response (IQuery<UserDto?>)
var mediator = provider.GetRequiredService<IMediator>();

var alice = await mediator.Send(new FindUserQuery("alice"));
Console.WriteLine($"Found user: {alice?.Name ?? "(null)"}");

var nobody = await mediator.Send(new FindUserQuery("missing"));
Console.WriteLine($"Missing user: {nobody?.Name ?? "(null)"}");
