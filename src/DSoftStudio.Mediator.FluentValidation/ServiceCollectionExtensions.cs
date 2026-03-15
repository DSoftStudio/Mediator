// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.FluentValidation;

public static class FluentValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ValidationBehavior{TRequest,TResponse}"/> as an open-generic
    /// pipeline behavior. All <see cref="FluentValidation.IValidator{T}"/> instances resolved
    /// from DI will be executed before the handler.
    /// <para>
    /// Call this after <c>AddMediator()</c> / <c>RegisterMediatorHandlers()</c>
    /// and before <c>PrecompilePipelines()</c>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// services
    ///     .AddMediator()
    ///     .RegisterMediatorHandlers()
    ///     .AddMediatorFluentValidation()
    ///     .PrecompilePipelines();
    /// </code>
    /// </example>
    public static IServiceCollection AddMediatorFluentValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
