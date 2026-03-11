// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator
{
    public static class StreamPipelineInvoker
    {
        public static IAsyncEnumerable<TResponse> Invoke<TRequest, TResponse>(
            TRequest request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
            where TRequest : IStreamRequest<TResponse>
        {
            var factory = StreamDispatch<TRequest, TResponse>.Handler;

            if (factory == null)
                throw new InvalidOperationException(
                    $"Stream handler for {typeof(TRequest).Name} not registered.");

            var handler = factory(serviceProvider);

            var behaviors = serviceProvider
                .GetServices<IStreamPipelineBehavior<TRequest, TResponse>>()
                .ToArray();

            // Fast path: no behaviors — invoke handler directly, zero closures.
            if (behaviors.Length == 0)
                return handler.Handle(request, cancellationToken);

            // Reverse iteration via index — avoids LINQ .Reverse() allocation.
            IStreamRequestHandler<TRequest, TResponse> next = handler;

            for (int i = behaviors.Length - 1; i >= 0; i--)
            {
                var currentNext = next;
                var currentBehavior = behaviors[i];
                next = new StreamBehaviorHandlerAdapter<TRequest, TResponse>(currentBehavior, currentNext);
            }

            return next.Handle(request, cancellationToken);
        }
    }
}
