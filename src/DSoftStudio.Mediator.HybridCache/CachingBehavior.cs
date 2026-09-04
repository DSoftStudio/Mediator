// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;
using Cache = Microsoft.Extensions.Caching.Hybrid.HybridCache;

namespace DSoftStudio.Mediator.HybridCache;

/// <summary>
/// Pipeline behavior that caches results for requests implementing <see cref="ICachedRequest"/>.
/// <para>
/// Uses <c>HybridCache</c> (L1 memory + optional L2 distributed) with built-in
/// stampede prevention and serialization. When the request does not implement
/// <see cref="ICachedRequest"/>, the behavior is a no-op pass-through.
/// </para>
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Cache _cache;

    public CachingBehavior(Cache cache)
    {
        // Registered as a singleton, so this runs once per application — cheap enough to convert a
        // mis-wired container into an ArgumentNullException naming the parameter, instead of a bare
        // NullReferenceException on whichever request first turns out to be cacheable.
        ArgumentNullException.ThrowIfNull(cache);

        _cache = cache;
    }

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICachedRequest cached)
            return await next.Handle(request, cancellationToken);

        return await _cache.GetOrCreateAsync(
            cached.CacheKey,
            // The state overload keeps the factory delegate static: no closure is allocated per
            // dispatch, and the delegate is cached once by the compiler.
            new FactoryState(request, next, ExecutionContext.Capture()),
            static (state, token) => state.Invoke(token),
            new HybridCacheEntryOptions
            {
                Expiration = cached.Duration
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The downstream call plus the ExecutionContext it must run under.
    /// <para>
    /// A struct, so passing it as <c>HybridCache</c>'s <c>TState</c> costs nothing on a cache hit —
    /// where the factory is never invoked at all.
    /// </para>
    /// </summary>
    private readonly struct FactoryState(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        ExecutionContext? callerContext)
    {
        /// <summary>
        /// Runs the rest of the pipeline under the ExecutionContext the caller of <c>Send</c> was on.
        /// <para>
        /// <c>DefaultHybridCache.GetOrCreateAsync</c> tests <c>cancellationToken.CanBeCanceled</c> and,
        /// when it is true, hands the factory to the thread pool via
        /// <c>ThreadPool.UnsafeQueueUserWorkItem</c> — the overload that captures NO ExecutionContext.
        /// Without the restore below, a cache miss on a real (cancellable) request token runs the
        /// handler with <c>Activity.Current</c> and <c>IHttpContextAccessor.HttpContext</c> null: the
        /// trace detaches from the request, and a handler that reads its tenant or user from ambient
        /// state computes the wrong answer — which is then cached for the whole TTL.
        /// </para>
        /// <para>
        /// Passing <see cref="CancellationToken.None"/> to <c>GetOrCreateAsync</c> would dodge that
        /// queueing path, but at the price of the caller's ability to abandon a slow factory, so the
        /// caller's token is forwarded unchanged and the context is restored here instead.
        /// </para>
        /// </summary>
        public ValueTask<TResponse> Invoke(CancellationToken cancellationToken)
        {
            // Capture answers null when the caller was already on the default context: there is then
            // nothing to restore, and ExecutionContext.Run rejects a null context outright.
            if (callerContext is null)
                return next.Handle(request, cancellationToken);

            var invocation = new Invocation(request, next, cancellationToken);
            ExecutionContext.Run(callerContext, static state => ((Invocation)state!).Start(), invocation);
            return invocation.Pending;
        }
    }

    /// <summary>
    /// Carries the downstream call across <see cref="ExecutionContext.Run"/>, whose callback returns
    /// void and so cannot hand the task back. Allocated only on a cache MISS — a hit never reaches
    /// the factory.
    /// </summary>
    private sealed class Invocation(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        public ValueTask<TResponse> Pending { get; private set; }

        /// <summary>
        /// Started INSIDE <see cref="ExecutionContext.Run"/> so the handler's async state machine
        /// captures the caller's context at its first suspension point. Awaiting the result
        /// afterwards, outside the Run, is then immaterial: the context that mattered has already
        /// been flowed into the machine.
        /// </summary>
        public void Start() => Pending = next.Handle(request, cancellationToken);
    }
}
