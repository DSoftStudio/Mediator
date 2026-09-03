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
    internal sealed class Mediator : IMediator, IServiceProviderAccessor
    {
        internal readonly IServiceProvider _serviceProvider;
        private readonly INotificationPublisher? _notificationPublisher;

        public Mediator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _notificationPublisher = serviceProvider.GetService<INotificationPublisher>();

            // Resolving it is the whole point: it is Scoped, so this hands the container an
            // IDisposable tied to THIS scope, whose disposal releases the dispatch cache slots that
            // would otherwise keep the scope alive. Once per scope, off any hot path.
            serviceProvider.GetService<MediatorScopeRelease>();

            // Set the global static flag once so interceptors can skip the per-call
            // GetService<INotificationPublisher> probe (~2-3 ns saved per Publish call).
            if (_notificationPublisher is not null)
                NotificationPublisherFlag.MarkRegistered();

            // Same probe, same reason, for the observation port: asking once per scope is what buys
            // every dispatch the right to answer "is anyone observing?" with a static read.
            if (serviceProvider.GetService<IMediatorNotificationObserver>() is not null)
                NotificationPublisherFlag.ObserverAppeared();
        }

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
                var chain = PipelineChainCache<TRequest, TResponse>.Resolve(_serviceProvider);
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

            // `Publish(n)` where n is declared as INotification, or as an abstract base, instantiates
            // this method at that static type -- which has no dispatch table of its own, so it would
            // report a publish and invoke nothing. Dispatch by the RUNTIME type instead.
            //
            // typeof(TNotification).IsAbstract is a JIT constant per instantiation, so a concrete
            // TNotification folds the whole branch away and pays nothing. The table check is what
            // keeps an abstract base that DOES have handlers of its own going to those handlers
            // rather than being re-routed.
            if (typeof(TNotification).IsAbstract
                && NotificationDispatch<TNotification>.Handlers is not { Length: > 0 })
            {
                return NotificationObjectDispatch.Dispatch(
                    notification, _serviceProvider, _notificationPublisher, cancellationToken);
            }

            // One static read on the plain path. Both questions -- is there a publisher, is there an
            // observer -- are settled at startup, because both registrations are knowable before the
            // container is built. A process with neither never reaches the routed entry at all, and
            // the routed entry carries the publisher branch this method used to inline.
            return NotificationPublisherFlag.NotPlain
                ? NotificationCachedDispatcher.DispatchRouted(notification, _serviceProvider, cancellationToken)
                : NotificationCachedDispatcher.DispatchSequential(notification, _serviceProvider, cancellationToken);
        }

        /// <inheritdoc />
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notification);

            // AOT-safe: uses compile-time generated type switch (or FrozenDictionary fallback)
            // populated by NotificationRegistry.Register().
            // No INotification type check here — the dispatch table rejects unknown types,
            // keeping this method free of IL throw instructions for JIT inline-friendliness.
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

