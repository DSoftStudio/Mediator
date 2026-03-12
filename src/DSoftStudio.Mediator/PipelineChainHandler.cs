// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Zero-allocation pipeline executor using interface dispatch and index-based chain traversal.
    /// <para>
    /// <b>Architecture:</b> This class implements <see cref="IRequestHandler{TRequest, TResponse}"/>
    /// so it can pass <c>this</c> as the <c>next</c> parameter to each behavior.
    /// Behaviors call <c>next.Handle(request, ct)</c> which routes back to <see cref="InvokeNext"/>
    /// via interface dispatch (virtual call) — no delegates, no closures.
    /// </para>
    /// <para>
    /// <b>How it works:</b> <c>Handle()</c> stores the per-request state (<c>request</c>,
    /// <c>cancellationToken</c>) in fields and resets <c>_behaviorIndex</c> to 0.
    /// <c>InvokeNext()</c> advances the index and calls the next behavior or handler.
    /// Each behavior receives <c>this</c> as an <see cref="IRequestHandler{TRequest, TResponse}"/>,
    /// and calling <c>next.Handle(request, ct)</c> is a virtual call (~0.5 ns) instead of
    /// a delegate invocation (~2 ns).
    /// </para>
    /// <para>
    /// <b>Reentrancy:</b> If a behavior or handler triggers a nested <c>Send()</c> of the
    /// same request type on the same scope, the <c>_active</c> flag detects it and falls
    /// back to a closure-based chain (correct but allocating).
    /// </para>
    /// <para>
    /// <b>Sync fast path:</b> When the entire chain completes synchronously
    /// (common for in-memory handlers), the <c>IsCompletedSuccessfully</c> check
    /// avoids the async state machine allocation entirely.
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

        public PipelineChainHandler(
            IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors,
            IRequestHandler<TRequest, TResponse> handler,
            IEnumerable<IRequestPreProcessor<TRequest>> preProcessors,
            IEnumerable<IRequestPostProcessor<TRequest, TResponse>> postProcessors,
            IEnumerable<IRequestExceptionHandler<TRequest, TResponse>> exceptionHandlers)
        {
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
            => _pipelineMode switch
            {
                0 => _handler.Handle(request, cancellationToken),
                1 => HandleBehaviorsOnly(request, cancellationToken),
                _ => HandleFull(request, cancellationToken),
            };

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
