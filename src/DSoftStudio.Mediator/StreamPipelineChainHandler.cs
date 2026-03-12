// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Pre-wired stream pipeline executor — analogous to <see cref="PipelineChainHandler{TRequest, TResponse}"/>
    /// for requests. Pre-links the behavior chain at construction (per scope) so the hot path is
    /// a single <c>_prelinkedChain.Handle()</c> call — zero mutable state, zero index tracking.
    /// </summary>
    public sealed class StreamPipelineChainHandler<TRequest, TResponse>
        : IStreamRequestHandler<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        private readonly IStreamRequestHandler<TRequest, TResponse> _handler;
        private readonly IStreamRequestHandler<TRequest, TResponse> _prelinkedChain;

        public StreamPipelineChainHandler(
            IStreamRequestHandler<TRequest, TResponse> handler,
            IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>> behaviors)
        {
            _handler = handler;

            var behaviorArray = behaviors is IStreamPipelineBehavior<TRequest, TResponse>[] arr
                ? arr
                : behaviors.ToArray();

            // Pre-link behavior chain: adapter0 → adapter1 → ... → handler.
            // Built once at construction (per scope). Zero mutable state on hot path.
            IStreamRequestHandler<TRequest, TResponse> chain = _handler;
            for (int i = behaviorArray.Length - 1; i >= 0; i--)
                chain = new StreamBehaviorHandlerAdapter<TRequest, TResponse>(behaviorArray[i], chain);
            _prelinkedChain = chain;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
            => _prelinkedChain.Handle(request, cancellationToken);

        /// <summary>
        /// <see cref="IStreamRequestHandler{TRequest, TResponse}"/> explicit implementation.
        /// Routes to the pre-linked chain.
        /// </summary>
        IAsyncEnumerable<TResponse> IStreamRequestHandler<TRequest, TResponse>.Handle(
            TRequest request, CancellationToken cancellationToken)
            => Handle(request, cancellationToken);
    }

    internal sealed class StreamBehaviorHandlerAdapter<TRequest, TResponse>(
        IStreamPipelineBehavior<TRequest, TResponse> behavior,
        IStreamRequestHandler<TRequest, TResponse> next) : IStreamRequestHandler<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        public IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
            => behavior.Handle(request, next, cancellationToken);
    }
}
