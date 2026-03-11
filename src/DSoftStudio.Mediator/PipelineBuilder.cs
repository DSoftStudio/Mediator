// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator
{

    /// <summary>
    /// Builds a reusable pipeline delegate for a given <c>&lt;TRequest, TResponse&gt;</c> pair.
    /// <para>
    /// <b>No behaviors:</b> The delegate resolves the handler from DI and invokes it directly.
    /// Zero closures, zero intermediate allocations.
    /// </para>
    /// <para>
    /// <b>With behaviors:</b> The delegate resolves a pre-wired
    /// <see cref="PipelineChainHandler{TRequest, TResponse}"/> from DI.
    /// The chain is built once per DI scope (not per request), eliminating
    /// the per-request closure fold that previously created N closures + 1 array.
    /// </para>
    /// Public to allow compile-time generated code (<c>MediatorRegistry</c>) to initialize pipelines.
    /// </summary>
    public static class PipelineBuilder
    {
        /// <summary>
        /// Constructs a compiled pipeline delegate that can be cached and reused.
        /// When no behaviors are registered, the delegate goes straight to the handler
        /// with zero allocations on the hot path.
        /// When behaviors exist, the chain is resolved as a single pre-wired service from DI.
        /// </summary>
        public static Func<TRequest, IServiceProvider, CancellationToken, ValueTask<TResponse>>
            Build<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return static (request, serviceProvider, cancellationToken) =>
            {
                // Try to resolve the pre-wired pipeline chain first.
                // When behaviors/processors are registered, PrecompilePipelines() adds
                // PipelineChainHandler to DI — it caches the handler + behaviors array
                // at scope creation (zero GetServices/ToArray per request).
                var chain = serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();

                if (chain is not null)
                    return chain.Handle(request, cancellationToken);

                // No PipelineChainHandler in DI — no pipeline features registered.
                // Go directly to the handler: single DI lookup, zero overhead.
                return serviceProvider
                    .GetRequiredService<IRequestHandler<TRequest, TResponse>>()
                    .Handle(request, cancellationToken);
            };
        }
    }
}
