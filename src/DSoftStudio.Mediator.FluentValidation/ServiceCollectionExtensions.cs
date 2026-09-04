// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <para>
    /// Calling it more than once — a shared library and the host application both wiring validation
    /// up — registers the behavior only once. A second descriptor would land in every chain as well,
    /// so every validator would run twice on every dispatch.
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

        // Scoped, not Transient: a Transient pipeline component makes the generated RegisterPipeline
        // register the whole request's PipelineChainHandler as Transient and skip
        // MarkPipelineChainCacheable, so EVERY dispatch of EVERY request in the application re-resolves
        // and re-links the chain from DI instead of reusing the per-scope one (measured 107.00 ns / 200 B
        // per dispatch against 80.10 ns / 24 B when cacheable). Not Singleton either — FluentValidation's
        // documented default validator lifetime is Scoped, and a singleton behavior would capture those
        // validators, and their scoped dependencies, for the life of the process.
        //
        // TryAddEnumerable, not Add: it dedups on (service type, implementation type), so a library and
        // its host can both call this without the behavior landing in the chain twice.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));

        return services;
    }

    /// <summary>
    /// Registers validation for ONE request type instead of for every request in the application.
    /// <para>
    /// The parameterless overload registers <see cref="ValidationBehavior{TRequest,TResponse}"/> as an
    /// OPEN generic, so it lands in the chain of every request — including the ones that have no
    /// validator at all, which then get a pipeline chain built for them and lose the direct handler
    /// path they would otherwise keep. This overload registers the behavior closed over one
    /// (request, response) pair, which the generated pipeline registration matches for that pair
    /// alone.
    /// </para>
    /// <para>
    /// Use it when validation covers a known handful of requests. Prefer the open-generic overload
    /// when a request having a validator is the norm, or when validators are discovered by assembly
    /// scanning and the set is not known at the registration site — a request whose pair is not
    /// registered here is NOT validated, even if a validator for it exists.
    /// </para>
    /// <para>
    /// Behaviors run in registration order. The two overloads are alternatives: if the open-generic
    /// one has already been called, this is a no-op, because that registration already covers this
    /// pair and a second descriptor would run every validator twice.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// services
    ///     .AddMediator()
    ///     .RegisterMediatorHandlers()
    ///     .AddMediatorFluentValidation&lt;CreateUser, Guid&gt;()
    ///     .PrecompilePipelines();
    /// </code>
    /// </example>
    public static IServiceCollection AddMediatorFluentValidation<TRequest, TResponse>(this IServiceCollection services)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        // The open registration is a superset: the generated startup closes it over every pair,
        // this one included, so a second descriptor here would run every validator twice.
        if (HasOpenRegistration(services))
            return services;

        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IPipelineBehavior<TRequest, TResponse>, ValidationBehavior<TRequest, TResponse>>());

        return services;
    }

    private static bool HasOpenRegistration(IServiceCollection services)
    {
        for (int i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IPipelineBehavior<,>)
                && descriptor.ImplementationType == typeof(ValidationBehavior<,>))
                return true;
        }

        return false;
    }
}
