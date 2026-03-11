// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Default <see cref="IMediator"/> implementation.
    /// <para>
    /// All dispatch is static-generic — single CLR field lookup per call.
    /// No runtime reflection, no dictionary lookup, no wrapper allocation.
    /// </para>
    /// Thread-safe and stateless — safe to register as scoped.
    /// </summary>
    internal sealed class Mediator(IServiceProvider serviceProvider) : IMediator, IServiceProviderAccessor
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly INotificationPublisher? _notificationPublisher = serviceProvider.GetService<INotificationPublisher>();

        /// <summary>Notification wrapper cache for <see cref="Publish(object, CancellationToken)"/>.</summary>
        private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> NotificationWrapperCache = new();

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
                var chain = _serviceProvider.GetService<PipelineChainHandler<TRequest, TResponse>>();
                if (chain is not null)
                    return chain.Handle(request, cancellationToken);
            }

            return _serviceProvider
                .GetRequiredService<IRequestHandler<TRequest, TResponse>>()
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

            return NotificationDispatcher.DispatchSequential(notification, _serviceProvider, cancellationToken);
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

            var notificationType = notification.GetType();
            var wrapper = NotificationWrapperCache.GetOrAdd(notificationType, static nt =>
            {
                var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(nt);
                return (NotificationHandlerWrapper)CompileFactory(wrapperType)();
            });

            return wrapper.Handle(notification, _serviceProvider, _notificationPublisher, cancellationToken);
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

        // ── Shared helper ─────────────────────────────────────────────────

        private static Func<object> CompileFactory(Type wrapperType)
        {
            var ctor = wrapperType.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes)
                ?? throw new InvalidOperationException($"No parameterless constructor found on {wrapperType.Name}.");

            var newExpr = Expression.New(ctor);
            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(newExpr, typeof(object)));
            return lambda.Compile();
        }
    }

}

