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
            // Same message as StreamHandlerCache: both reach this state the same way (PrecompileStreams
            // never ran), and they used to fail differently — one with a diagnostic, the other with a
            // bare NRE from a null-forgiving invoke.
            var factory = StreamDispatch<TRequest, TResponse>.Handler
                ?? throw new InvalidOperationException(
                    $"Stream handler for {typeof(TRequest).Name} not registered. " +
                    "Ensure PrecompileStreams() is called during service configuration.");

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
                next = new StreamBehaviorHandlerAdapter<TRequest, TResponse>(behaviors[i], next);
            }

            return next.Handle(request, cancellationToken);
        }
    }
}
