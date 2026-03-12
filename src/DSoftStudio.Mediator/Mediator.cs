// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Default <see cref="IMediator"/> implementation.
    /// <para>
    /// All dispatch is static-generic — single CLR field lookup per call.
    /// No runtime reflection, no dictionary lookup, no wrapper allocation.
    /// Fully AOT and trimming compatible.
    /// </para>
    /// Thread-safe and stateless — safe to register as scoped.
    /// </summary>
    internal sealed class Mediator(IServiceProvider serviceProvider) : IMediator, IServiceProviderAccessor
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly INotificationPublisher? _notificationPublisher = serviceProvider.GetService<INotificationPublisher>();

        /// <inheritdoc />
        IServiceProvider IServiceProviderAccessor.ServiceProvider => _serviceProvider;

        // ── Send ──────────────────────────────────────────────────────────

        /// <inheritdoc />
        public ValueTask<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
        {
            ArgumentNullException.ThrowIfNull(request);

            // Zero-delegate dispatch: static bool skips the GetService probe for the
            // no-behaviors path. GetService (not GetRequiredService) provides safe fallback
            // when static flag and DI container are out of sync (e.g. test isolation).
            if (RequestDispatch<TRequest, TResponse>.HasPipelineChain)
            {
                var chain = RequestDispatch<TRequest, TResponse>.IsPipelineChainCacheable
                    ? PipelineChainCache<TRequest, TResponse>.Resolve(_serviceProvider)
                    : _serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();
                if (chain is not null)
                    return chain.Handle(request, cancellationToken);
            }

            return HandlerCache<TRequest, TResponse>
                .Resolve(_serviceProvider)
                .Handle(request, cancellationToken);
        }

        // ── Notifications ─────────────────────────────────────────────────

        /// <inheritdoc />
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            ArgumentNullException.ThrowIfNull(notification);

            if (_notificationPublisher is not null)
            {
                var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();
                return _notificationPublisher.Publish(handlers, notification, cancellationToken);
            }

            return NotificationCachedDispatcher.DispatchSequential(notification, _serviceProvider, cancellationToken);
        }

        /// <inheritdoc />
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notification);

            if (notification is not INotification)
            {
                throw new ArgumentException(
                    $"Object of type {notification.GetType().Name} does not implement {nameof(INotification)}.",
                    nameof(notification));
            }

            // AOT-safe: uses compile-time generated dispatch table populated by
            // NotificationRegistry.Register(). No MakeGenericType, no Expression.Compile.
            return NotificationObjectDispatch.Dispatch(
                notification, _serviceProvider, _notificationPublisher, cancellationToken);
        }

        // ── Streaming ──────────────────────────────────────────────────

        /// <inheritdoc />
        public IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IStreamRequest<TResponse>
        {
            ArgumentNullException.ThrowIfNull(request);

            // O(1) CLR generic static field lookup — populated by PrecompileStreams().
            var pipeline = StreamDispatch<TRequest, TResponse>.Pipeline;
            if (pipeline is not null)
                return pipeline(request, _serviceProvider, cancellationToken);

            // Fallback for non-precompiled streams.
            return StreamPipelineInvoker.Invoke<TRequest, TResponse>(
                request, _serviceProvider, cancellationToken);
        }
    }

}

