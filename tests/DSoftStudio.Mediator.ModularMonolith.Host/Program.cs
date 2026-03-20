// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

// ============================================================================
// Modular Monolith Reproduction — Host (Composition Root)
// ============================================================================
// This project references the source generators.
// The generators will discover GetWeatherQueryHandler from the Module via
// Phase 2 (type-based fallback) and attempt to generate DI registration
// code referencing the INTERNAL type → expected CS0122.
// ============================================================================

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.ModularMonolith.Module;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddMediator()
    .RegisterMediatorHandlers();

using var provider = services.BuildServiceProvider();

var mediator = provider.GetRequiredService<DSoftStudio.Mediator.Abstractions.IMediator>();

var result = await mediator.Send<GetWeatherQuery, string>(new GetWeatherQuery());
Console.WriteLine($"Weather: {result}");
