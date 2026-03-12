// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

[assembly: Mediator.MediatorOptions(ServiceLifetime = ServiceLifetime.Singleton)]

namespace Benchmarks;

/// <summary>
/// Thin wrapper to call martinothamar/Mediator's source-generated AddMediator
/// without colliding with DSoftStudio.Mediator.ServiceCollectionExtensions.AddMediator.
/// </summary>
internal static class MediatorSGHelper
{
    internal static IServiceCollection AddMediatorSG(this IServiceCollection services)
        => services.AddMediator();
}
