// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DSoftStudio.Mediator.HybridCache;

public static class HybridCacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="CachingBehavior{TRequest,TResponse}"/> as an open-generic
    /// pipeline behavior. Requests that implement <see cref="ICachedRequest"/> will have
    /// their results cached via <c>HybridCache</c>; all other requests pass through.
    /// <para>
    /// Requires <c>AddHybridCache()</c> to be called before or after this method.
    /// Call this after <c>AddMediator()</c> / <c>RegisterMediatorHandlers()</c>
    /// and before <c>PrecompilePipelines()</c>.
    /// </para>
    /// <para>
    /// Idempotent: calling it twice registers one behavior, not two nested ones.
    /// </para>
    /// <para>
    /// <b>Under Native AOT or trimming, every cached response type needs a serializer.</b>
    /// <c>HybridCache</c> serializes everything it caches, and <c>AddHybridCache</c> pre-registers a
    /// serializer for exactly two types — <see langword="string"/> and <c>byte[]</c>. Anything else
    /// falls back to reflection-based <c>System.Text.Json</c>, which those modes disable. This
    /// package's own code is AOT-safe and the publish succeeds either way, so the failure lands on
    /// the first dispatch of a cacheable request in the published application rather than at build
    /// time. Declare a context listing the cached response types and register it keyed on the open
    /// generic:
    /// <code>
    /// [JsonSerializable(typeof(ProductDto))]
    /// internal sealed partial class AppJsonContext : JsonSerializerContext;
    ///
    /// services.AddKeyedSingleton&lt;JsonSerializerOptions&gt;(
    ///     typeof(IHybridCacheSerializer&lt;&gt;),
    ///     new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default });
    /// </code>
    /// Both details matter: without the explicit <c>&lt;JsonSerializerOptions&gt;</c> argument the
    /// call is ambiguous and will not compile, and a non-keyed registration is silently ignored.
    /// </para>
    /// <para>
    /// <c>[ImmutableObject(true)]</c> is NOT a workaround, though it looks like one. Verified in a
    /// native binary: a sealed record carrying it throws exactly like an unmarked one, because the
    /// serializer-free path requires writes disabled on both cache tiers — which caches nothing.
    /// What the marker does change is that callers receive the SAME instance rather than a copy per
    /// read, so on a DTO anything later mutates it silently shares that mutation for the whole TTL.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// services
    ///     .AddMediator()
    ///     .RegisterMediatorHandlers()
    ///     .AddHybridCache()
    ///     .AddMediatorHybridCache()
    ///     .PrecompilePipelines();
    /// </code>
    /// </example>
    public static IServiceCollection AddMediatorHybridCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Singleton, not Transient. A transient pipeline component forces the generated
        // RegisterPipeline to register the PipelineChainHandler as Transient too, which clears the
        // cacheable flag and makes every single dispatch re-resolve and re-link the whole behavior
        // chain (measured: 194-206 ns / 208 B versus 148 ns / 32 B). Singleton is safe here because
        // the behavior's only dependency is HybridCache, which AddHybridCache registers as a
        // singleton — so it captures nothing scoped and stays resolvable from the root provider,
        // which is also what keeps an all-singleton application's chain cacheable.
        //
        // TryAddEnumerable, not Add: a second AddMediatorHybridCache() would otherwise nest this
        // behavior inside itself, and the outer GetOrCreateAsync factory would re-enter the inner
        // one on the SAME key while the outer call is still in flight. It dedups on
        // (ServiceType, ImplementationType), which is exactly this open-generic pair.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>)));

        return services;
    }

    /// <summary>
    /// Registers caching for ONE request type instead of for every request in the application.
    /// <para>
    /// The parameterless overload registers <see cref="CachingBehavior{TRequest,TResponse}"/> as an
    /// OPEN generic, so it lands in the chain of every request — and a request that never caches then
    /// gets a pipeline chain built for it and loses the direct handler path it would otherwise keep.
    /// This overload registers the behavior closed over one (request, response) pair, which the
    /// generated pipeline registration matches for that pair alone. Every other request is untouched.
    /// </para>
    /// <para>
    /// <typeparamref name="TRequest"/> must implement <see cref="ICachedRequest"/>. Registering
    /// caching for a request that never opted in would build a chain to run a behavior that always
    /// passes through, and here the compiler can say so — the open-generic form cannot.
    /// </para>
    /// <para>
    /// Behaviors run in registration order, so call this where caching belongs relative to the
    /// application's other pipeline components. The two overloads are alternatives: if the
    /// open-generic one has already been called, this is a no-op, because that registration already
    /// covers this pair and a second descriptor would put the behavior in the chain twice.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// services
    ///     .AddMediator()
    ///     .RegisterMediatorHandlers()
    ///     .AddHybridCache()
    ///     .AddMediatorHybridCache&lt;GetProduct, ProductDto&gt;()
    ///     .PrecompilePipelines();
    /// </code>
    /// </example>
    public static IServiceCollection AddMediatorHybridCache<TRequest, TResponse>(this IServiceCollection services)
        where TRequest : IRequest<TResponse>, ICachedRequest
    {
        ArgumentNullException.ThrowIfNull(services);

        // The open registration is a superset of this one: the generated startup splices it into a
        // closed descriptor for EVERY pair, this pair included. Adding another here would run the
        // behavior twice on the same key -- the nesting TryAddEnumerable exists to prevent.
        if (HasOpenRegistration(services))
            return services;

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPipelineBehavior<TRequest, TResponse>, CachingBehavior<TRequest, TResponse>>());

        return services;
    }

    private static bool HasOpenRegistration(IServiceCollection services)
    {
        for (int i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IPipelineBehavior<,>)
                && descriptor.ImplementationType == typeof(CachingBehavior<,>))
                return true;
        }

        return false;
    }
}
