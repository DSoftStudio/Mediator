// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Zero-allocation pipeline executor. Pre-links the behavior chain once at construction
    /// (per DI scope) into an immutable chain of <see cref="BehaviorHandlerAdapter{TRequest, TResponse}"/>
    /// ending at the terminal handler, so the hot path carries zero mutable state.
    /// <para>
    /// <b>Dispatch mode</b> is computed once in the constructor (see <c>ComputePipelineMode</c>):
    /// <list type="bullet">
    /// <item><description><b>PassThrough</b> (no components): calls the handler directly.</description></item>
    /// <item><description><b>BehaviorsOnly</b>: invokes the pre-linked behavior chain.</description></item>
    /// <item><description><b>Full</b>: pre-processors, post-processors and exception handlers around the chain.</description></item>
    /// </list>
    /// Each behavior receives the next link as an <see cref="IRequestHandler{TRequest, TResponse}"/>, so
    /// <c>next.Handle(request, ct)</c> is a virtual call (~0.5 ns) rather than a delegate invocation (~2 ns).
    /// Because the chain is immutable and stateless, nested / reentrant <c>Send()</c> calls are inherently safe.
    /// </para>
    /// <para>
    /// <b>Sync fast path:</b> when the chain completes synchronously (common for in-memory handlers),
    /// the <c>IsCompletedSuccessfully</c> checks avoid the async state-machine allocation entirely.
    /// </para>
    /// </summary>
    public sealed class PipelineChainHandler<TRequest, TResponse>
        : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IPipelineBehavior<TRequest, TResponse>[] _behaviors;
        private readonly IRequestHandler<TRequest, TResponse> _handler;
        private readonly IRequestPreProcessor<TRequest>[] _preProcessors;
        private readonly IRequestPostProcessor<TRequest, TResponse>[] _postProcessors;
        private readonly IRequestExceptionHandler<TRequest, TResponse>[] _exceptionHandlers;
        private readonly byte _pipelineMode; // 0=PassThrough, 1=BehaviorsOnly, 2=Full
        private readonly IRequestHandler<TRequest, TResponse> _prelinkedChain;
        // Optional dispatch-observation port (Ports & Adapters). Null when no adapter is registered (the
        // common case) → the hot path never touches it. See IMediatorDispatchObserver.
        private readonly IMediatorDispatchObserver? _observer;

        public PipelineChainHandler(
            IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors,
            IRequestHandler<TRequest, TResponse> handler,
            IEnumerable<IRequestPreProcessor<TRequest>> preProcessors,
            IEnumerable<IRequestPostProcessor<TRequest, TResponse>> postProcessors,
            IEnumerable<IRequestExceptionHandler<TRequest, TResponse>> exceptionHandlers,
            // Resolved by DI to an EMPTY sequence when no adapter is registered (non-OTel apps) — so the
            // mediator carries no tracing dependency and _observer stays null.
            IEnumerable<IMediatorDispatchObserver> observers)
        {
            // First registered observer wins (one tracing adapter in practice). foreach+break avoids a LINQ
            // allocation; constructed once per scope, not on the hot path.
            IMediatorDispatchObserver? firstObserver = null;
            foreach (var obs in observers) { firstObserver = obs; break; }
            _observer = firstObserver;

            _behaviors = behaviors is IPipelineBehavior<TRequest, TResponse>[] bArray
                ? bArray
                : [.. behaviors];
            _handler = handler;
            _preProcessors = preProcessors is IRequestPreProcessor<TRequest>[] preArray
                ? preArray
                : [.. preProcessors];
            _postProcessors = postProcessors is IRequestPostProcessor<TRequest, TResponse>[] postArray
                ? postArray
                : [.. postProcessors];
            _exceptionHandlers = exceptionHandlers is IRequestExceptionHandler<TRequest, TResponse>[] exArray
                ? exArray
                : [.. exceptionHandlers];

            _pipelineMode = ComputePipelineMode(
                _behaviors.Length, _preProcessors.Length,
                _postProcessors.Length, _exceptionHandlers.Length);

            // Pre-link behavior chain: adapter0 → adapter1 → ... → handler.
            // Built once at construction (per scope). Zero mutable state on hot path.
            IRequestHandler<TRequest, TResponse> chain = _handler;
            for (int i = _behaviors.Length - 1; i >= 0; i--)
                chain = new BehaviorHandlerAdapter<TRequest, TResponse>(_behaviors[i], chain);
            _prelinkedChain = chain;
        }

        /// <summary>
        /// 0 = PassThrough (no pipeline components), 1 = BehaviorsOnly, 2 = Full.
        /// </summary>
        private static byte ComputePipelineMode(
            int behaviors, int pre, int post, int exceptions)
        {
            if (behaviors == 0 && pre == 0 && post == 0 && exceptions == 0)
                return 0; // PassThrough

            if (pre == 0 && post == 0 && exceptions == 0)
                return 1; // BehaviorsOnly

            return 2; // Full
        }

        /// <summary>
        /// Entry point — 3-way dispatch computed once at construction.
        /// PassThrough: direct handler call. BehaviorsOnly: straight to chain (no processor/exception checks).
        /// Full: processors + exception handlers + behaviors.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
        {
            // HOT path: the only cost the dispatch port adds to a non-OTel app is this single field-null check.
            // `_observer` is null → straight to HandleCore, whose switch the JIT inlines right here (both this
            // method and HandleCore are AggressiveInlining), so the dispatch stays as tight as the pre-observer
            // version. The `IsActive` interface call lives in the COLD HandleWithObserver, never in this method.
            return (_observer is null) ? HandleCore(request, cancellationToken) : HandleWithObserver(request, cancellationToken);
        }

        /// <summary>
        /// Cold path taken only when a dispatch observer is registered. Splits idle (registered but nothing
        /// listening → run the dispatch unobserved) from active (wrap the dispatch in an observation scope).
        /// <para>
        /// <see cref="MethodImplOptions.NoInlining"/> keeps the <c>IsActive</c> interface call and its branches
        /// OUT of <see cref="Handle"/>, so the hot path stays a single null check that inlines cleanly into the
        /// cached dispatch. (The measured difference vs. inlining IsActive into Handle is within benchmark
        /// noise; keeping it out is simply the cheaper-to-reason-about, robust-across-JITs default.)
        /// </para>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private ValueTask<TResponse> HandleWithObserver(TRequest request, CancellationToken cancellationToken)
            => _observer!.IsActive
                ? HandleObserved(request, cancellationToken)
                : HandleCore(request, cancellationToken);

        /// <summary>
        /// The single 3-way dispatch switch, shared by the hot path (<see cref="Handle"/> delegates here) and
        /// the cold observer paths. <see cref="MethodImplOptions.AggressiveInlining"/> lets the JIT inline the
        /// switch into <see cref="Handle"/>, so the delegation costs nothing on the non-observed fast path.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ValueTask<TResponse> HandleCore(TRequest request, CancellationToken cancellationToken)
            => _pipelineMode switch
            {
                0 => _handler.Handle(request, cancellationToken),
                1 => HandleBehaviorsOnly(request, cancellationToken),
                _ => HandleFull(request, cancellationToken),
            };

        /// <summary>
        /// Opens the dispatch-observation scope (e.g. an OpenTelemetry span) around the ENTIRE pipeline so
        /// pre-/post-processors — which run outside the behavior chain — nest under it and attribute to THIS
        /// dispatch (concurrency-safe per-dispatch identity). Taken only when an adapter is active, so it
        /// never touches the non-observed hot path.
        /// </summary>
        private async ValueTask<TResponse> HandleObserved(TRequest request, CancellationToken cancellationToken)
        {
            // scope may be null when the adapter declined this dispatch (filtered / sampled out) — the
            // null-conditional calls below then no-op, so the dispatch runs exactly like the fast path.
            // (IsActive was already checked in HandleWithObserver before we got here.)
            var scope = _observer!.BeginDispatch<TRequest, TResponse>(request, _handler);
            try
            {
                return await HandleCore(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Only exceptions that propagated past EVERY component (incl. exception handlers) reach here —
                // i.e. the dispatch genuinely failed. `throw;` preserves the original stack.
                scope?.OnError(ex);
                throw;
            }
            finally
            {
                scope?.Dispose();
            }
        }

        /// <summary>
        /// Hot path for behaviors-only (no processors, no exception handlers).
        /// Calls the pre-linked chain directly — no array access, no index, no mutable state.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ValueTask<TResponse> HandleBehaviorsOnly(TRequest request, CancellationToken cancellationToken)
            => _prelinkedChain.Handle(request, cancellationToken);

        /// <summary>
        /// Full pipeline: processors + exception handlers + behaviors.
        /// </summary>
        private ValueTask<TResponse> HandleFull(TRequest request, CancellationToken cancellationToken)
        {
            if (_preProcessors.Length > 0 || _postProcessors.Length > 0)
                return HandleWithProcessors(request, cancellationToken);

            if (_exceptionHandlers.Length > 0)
                return HandleWithExceptionHandlers(request, cancellationToken);

            return HandleBehaviorsOnly(request, cancellationToken);
        }

        private ValueTask<TResponse> HandleWithProcessors(TRequest request, CancellationToken cancellationToken)
        {
            // Sync fast path: if all pre-processors complete synchronously,
            // execute core + post-processors without async state machine.
            for (int i = 0; i < _preProcessors.Length; i++)
            {
                var task = _preProcessors[i].Process(request, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    return HandleWithProcessorsAsync(request, i, task, cancellationToken);
            }

            var coreResult = _exceptionHandlers.Length > 0
                ? HandleWithExceptionHandlers(request, cancellationToken)
                : HandleBehaviorsOnly(request, cancellationToken);

            if (_postProcessors.Length == 0)
                return coreResult;

            if (!coreResult.IsCompletedSuccessfully)
                return AwaitCoreAndRunPostProcessors(request, coreResult, cancellationToken);

            var response = coreResult.Result;
            for (int i = 0; i < _postProcessors.Length; i++)
            {
                var task = _postProcessors[i].Process(request, response, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    return AwaitPostProcessorAndContinue(request, response, i, task, cancellationToken);
            }

            return new ValueTask<TResponse>(response);
        }

        private async ValueTask<TResponse> HandleWithProcessorsAsync(
            TRequest request, int preIndex, ValueTask pendingPreTask,
            CancellationToken cancellationToken)
        {
            await pendingPreTask.ConfigureAwait(false);

            for (int i = preIndex + 1; i < _preProcessors.Length; i++)
            {
                var task = _preProcessors[i].Process(request, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    await task.ConfigureAwait(false);
            }

            var response = _exceptionHandlers.Length > 0
                ? await HandleWithExceptionHandlers(request, cancellationToken).ConfigureAwait(false)
                : await HandleBehaviorsOnly(request, cancellationToken).ConfigureAwait(false);

            for (int i = 0; i < _postProcessors.Length; i++)
            {
                var task = _postProcessors[i].Process(request, response, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    await task.ConfigureAwait(false);
            }

            return response;
        }

        private async ValueTask<TResponse> AwaitCoreAndRunPostProcessors(
            TRequest request, ValueTask<TResponse> coreTask, CancellationToken cancellationToken)
        {
            var response = await coreTask.ConfigureAwait(false);

            for (int i = 0; i < _postProcessors.Length; i++)
            {
                var task = _postProcessors[i].Process(request, response, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    await task.ConfigureAwait(false);
            }

            return response;
        }

        private async ValueTask<TResponse> AwaitPostProcessorAndContinue(
            TRequest request, TResponse response,
            int postIndex, ValueTask pendingPostTask, CancellationToken cancellationToken)
        {
            await pendingPostTask.ConfigureAwait(false);

            for (int i = postIndex + 1; i < _postProcessors.Length; i++)
            {
                var task = _postProcessors[i].Process(request, response, cancellationToken);
                if (!task.IsCompletedSuccessfully)
                    await task.ConfigureAwait(false);
            }

            return response;
        }

        private async ValueTask<TResponse> HandleWithExceptionHandlers(TRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return await HandleBehaviorsOnly(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var state = new RequestExceptionHandlerState<TResponse>();

                for (int i = 0; i < _exceptionHandlers.Length; i++)
                {
                    var task = _exceptionHandlers[i].Handle(request, ex, state, cancellationToken);
                    if (!task.IsCompletedSuccessfully)
                        await task.ConfigureAwait(false);

                    if (state.Handled)
                        return state.Response!;
                }

                throw;
            }
        }

        /// <summary>
        /// <see cref="IRequestHandler{TRequest, TResponse}"/> implementation.
        /// Forwards to the pre-linked chain for behaviors, or directly to handler for passthrough.
        /// </summary>
        ValueTask<TResponse> IRequestHandler<TRequest, TResponse>.Handle(
            TRequest request, CancellationToken cancellationToken)
            => Handle(request, cancellationToken);
    }
}
