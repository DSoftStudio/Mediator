// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Pipeline behavior that wraps the execution of a request handler.
    /// Behaviors execute in registration order, forming a chain:
    /// <c>Behavior1 → Behavior2 → … → Handler</c>.
    /// <para>
    /// The <c>next</c> parameter is an <see cref="IRequestHandler{TRequest, TResponse}"/>
    /// representing the next step in the pipeline (another behavior or the terminal handler).
    /// Call <c>next.Handle(request, cancellationToken)</c> to continue the chain.
    /// This uses virtual dispatch instead of delegate invocation for maximum performance.
    /// </para>
    /// Use for cross-cutting concerns: logging, validation, authorization, transactions, etc.
    /// </summary>
    public interface IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Runs around the next step of the pipeline for <paramref name="request"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call <c>next.Handle(request, cancellationToken)</c> to continue the chain. Returning
        /// without calling it stops the chain there: no later behavior runs and neither does the
        /// handler, which is how a behavior serves a cached or rejected result. Post-processors are
        /// NOT skipped by this — they sit outside the chain and receive whatever response the
        /// behavior returned.
        /// </para>
        /// <para>
        /// Register behaviors BEFORE <c>PrecompilePipelines()</c>. That call decides, per
        /// request/response pair, whether a pipeline chain is built at all: if the pair has no
        /// behavior, pre-processor, post-processor or exception handler registered by then, no chain
        /// exists and anything added afterwards never runs — silently, with no error. Calling
        /// <c>PrecompilePipelines()</c> a second time does not repair it.
        /// </para>
        /// <para>
        /// The scan records only that a chain is needed; the behaviors themselves are resolved from
        /// the container when the chain is constructed. So a behavior registered after the scan does
        /// run when the pair already had one — but the chain's lifetime was fixed by the scan, so a
        /// <c>Transient</c> behavior added late can end up constructed once and shared. Register the
        /// whole pipeline before the call.
        /// </para>
        /// <para>
        /// When the behavior does not await anything of its own, return a completed value rather
        /// than marking the method <c>async</c>: an <c>async</c> method that never suspends still
        /// builds an async state machine, and a behavior sits on every dispatch of its request type.
        /// </para>
        /// </remarks>
        /// <param name="request">The request travelling through the pipeline.</param>
        /// <param name="next">
        /// The next step: another behavior, or the terminal handler. This is an
        /// <see cref="IRequestHandler{TRequest, TResponse}"/> rather than a delegate so the call is
        /// virtual dispatch instead of delegate invocation.
        /// </param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        /// <returns>The response, either from <paramref name="next"/> or produced by this behavior.</returns>
        ValueTask<TResponse> Handle(TRequest request, IRequestHandler<TRequest, TResponse> next, CancellationToken cancellationToken);
    }
}


