// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Pre-wired stream pipeline executor — analogous to <see cref="PipelineChainHandler{TRequest, TResponse}"/>
    /// for requests. Implements <see cref="IStreamRequestHandler{TRequest, TResponse}"/> so it can pass
    /// <c>this</c> as the <c>next</c> parameter to each behavior — interface dispatch, no delegates.
    /// </summary>
    public sealed class StreamPipelineChainHandler<TRequest, TResponse>
        : IStreamRequestHandler<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        private readonly IStreamRequestHandler<TRequest, TResponse> _handler;
        private readonly IStreamPipelineBehavior<TRequest, TResponse>[] _behaviors;

        private TRequest _request = default!;
        private CancellationToken _ct;
        private int _behaviorIndex;
        private int _active;

        public StreamPipelineChainHandler(
            IStreamRequestHandler<TRequest, TResponse> handler,
            IEnumerable<IStreamPipelineBehavior<TRequest, TResponse>> behaviors)
        {
            _handler = handler;
            _behaviors = behaviors is IStreamPipelineBehavior<TRequest, TResponse>[] arr
                ? arr
                : behaviors.ToArray();
        }

        public IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
        {
            if (_behaviors.Length == 0)
                return _handler.Handle(request, cancellationToken);

            if (Interlocked.CompareExchange(ref _active, 1, 0) == 0)
            {
                _request = request;
                _ct = cancellationToken;
                _behaviorIndex = 0;

                var result = InvokeNext();

                _request = default!;
                Volatile.Write(ref _active, 0);

                return result;
            }

            return HandleWithClosures(request, cancellationToken);
        }

        /// <summary>
        /// <see cref="IStreamRequestHandler{TRequest, TResponse}"/> explicit implementation.
        /// Called by behaviors via <c>next.Handle(request, ct)</c> — interface dispatch.
        /// </summary>
        IAsyncEnumerable<TResponse> IStreamRequestHandler<TRequest, TResponse>.Handle(
            TRequest request, CancellationToken cancellationToken)
        {
            _request = request;
            _ct = cancellationToken;
            return InvokeNext();
        }

        private IAsyncEnumerable<TResponse> InvokeNext()
        {
            if (_behaviorIndex >= _behaviors.Length)
                return _handler.Handle(_request, _ct);

            var behavior = _behaviors[_behaviorIndex++];
            return behavior.Handle(_request, this, _ct);
        }

        private IAsyncEnumerable<TResponse> HandleWithClosures(TRequest request, CancellationToken ct)
        {
            IStreamRequestHandler<TRequest, TResponse> next = _handler;

            for (int i = _behaviors.Length - 1; i >= 0; i--)
            {
                var currentNext = next;
                var currentBehavior = _behaviors[i];
                next = new StreamBehaviorHandlerAdapter<TRequest, TResponse>(currentBehavior, currentNext);
            }

            return next.Handle(request, ct);
        }
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
