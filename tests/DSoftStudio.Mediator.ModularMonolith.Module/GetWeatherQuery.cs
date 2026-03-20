// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.ModularMonolith.Module;

/// <summary>
/// Public contract — visible to the host and other modules.
/// </summary>
public sealed record GetWeatherQuery : IQuery<string>;
