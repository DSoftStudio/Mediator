// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Notifications;

public sealed record ObservedPing : INotification;

public sealed class ObservedHandlerA : INotificationHandler<ObservedPing>
{
    public Task Handle(ObservedPing notification, CancellationToken ct) => Task.CompletedTask;
}

public sealed class ObservedHandlerB : INotificationHandler<ObservedPing>
{
    /// <summary>Set by the test that needs a handler which has not finished yet.</summary>
    public static TaskCompletionSource? Gate;

    public Task Handle(ObservedPing notification, CancellationToken ct)
        => Gate?.Task ?? Task.CompletedTask;
}

/// <summary>
/// ADR-0007: the core CALLS the observer; the observer never substitutes anything.
/// <para>
/// Joins the serialized AggressiveDispatch collection because registering an observer disarms the
/// notification fast path process-wide, exactly as a custom publisher does.
/// </para>
/// </summary>
[Collection("AggressiveDispatch")]
public class NotificationObservationTests
{
    private static void ResetTierState()
    {
        AggressiveDispatchLatch.ResetForTests();
        NotificationPublisherFlag.ResetForTests();
        ObservedHandlerB.Gate = null;
    }

    private static ServiceProvider Build(RecordingObserver? observer, INotificationPublisher? publisher = null)
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();

        if (observer is not null)
            services.AddSingleton<IMediatorNotificationObserver>(observer);

        if (publisher is not null)
            services.AddSingleton(publisher);

        services.PrecompileNotifications();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TheObserverSeesOneEnvelopeAndOneSubscriberPerHandler()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer);
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        observer.Publishes.ShouldBe(1);

        // The INSTANCE, not the interface: an adapter has to be able to read the concrete
        // subscriber, and a wrapper type would collapse them all into one.
        observer.Scope!.Subscribers.Select(s => s.Handler.GetType()).ShouldBe(
            new[] { typeof(ObservedHandlerA), typeof(ObservedHandlerB) }, ignoreOrder: true);

        observer.Scope.Subscribers.ShouldAllBe(s => s.Disposed);
        observer.Scope.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task ASubscriberScopeStaysOpenUntilThatHandlersTaskCompletes()
    {
        ResetTierState();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ObservedHandlerB.Gate = gate;

        var observer = new RecordingObserver();
        using var provider = Build(observer);

        var publish = provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        // B has been entered and has NOT finished. Closing its scope now would report a subscriber
        // that took no time at all — the failure this shape exists to prevent.
        var pending = observer.Scope!.Subscribers.Single(s => s.Handler is ObservedHandlerB);
        pending.Disposed.ShouldBeFalse();

        gate.SetResult();
        await publish;

        pending.Disposed.ShouldBeTrue();
        observer.Scope.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task WithACustomPublisherTheSubscribersAreStillObserved()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer, new SequentialNotificationPublisher());
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        // A publisher owns the loop, so the core cannot bracket the invocations from outside -- it
        // hands the publisher handlers that bracket themselves instead. Giving up here would lose the
        // whole per-subscriber breakdown for anyone who registered a publisher, which is exactly the
        // shape a profiler installs.
        observer.Publishes.ShouldBe(1);
        observer.Scope!.Subscribers.Select(s => s.Handler.GetType()).ShouldBe(
            new[] { typeof(ObservedHandlerA), typeof(ObservedHandlerB) }, ignoreOrder: true);
        observer.Scope.Subscribers.ShouldAllBe(s => s.Disposed);
        observer.Scope.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishingAsObjectIsObservedToo()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer);
        await provider.GetRequiredService<IMediator>()
            .Publish((object)new ObservedPing(), TestContext.Current.CancellationToken);

        // The object route used to have its own dispatch that never consulted the observer, so a
        // domain-event or outbox loop publishing as `object` was invisible.
        observer.Publishes.ShouldBe(1);
        observer.Scope!.Subscribers.Count.ShouldBe(2);
        observer.Scope.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task AParallelPublisherObservesEverySubscriberFromItsOwnThread()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer, new ParallelNotificationPublisher());
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        // The parallel publisher queues each handler to the thread pool, so BeginSubscriber runs
        // concurrently on the same scope. Losing one here would silently drop a subscriber from
        // every fan-out an application publishes in parallel.
        observer.Scope!.Subscribers.Count.ShouldBe(2);
        observer.Scope.Subscribers.Select(s => s.Handler.GetType()).ShouldBe(
            new[] { typeof(ObservedHandlerA), typeof(ObservedHandlerB) }, ignoreOrder: true);
        observer.Scope.Subscribers.ShouldAllBe(s => s.Disposed);
        observer.Scope.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task AnIdleObserverIsNotAskedToOpenAnything()
    {
        ResetTierState();
        var observer = new RecordingObserver { Active = false };

        using var provider = Build(observer);
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        observer.Publishes.ShouldBe(0);
    }

    // ── Recording double ──────────────────────────────────────────────

    internal sealed class RecordingObserver : IMediatorNotificationObserver
    {
        public bool Active = true;
        public int Publishes;
        public RecordingScope? Scope;

        public bool IsActive => Active;

        public IMediatorPublishScope? BeginPublish<TNotification>(TNotification notification)
            where TNotification : INotification
        {
            Publishes++;
            return Scope = new RecordingScope();
        }
    }

    internal sealed class RecordingScope : IMediatorPublishScope
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<RecordingSubscriber> _subscribers = new();

        public IReadOnlyCollection<RecordingSubscriber> Subscribers => _subscribers;
        public bool Disposed;
        public Exception? Error;

        // Concurrent by contract: a publisher may run the handlers in parallel, so this is called
        // from several threads for the same scope.
        public IMediatorSubscriberScope? BeginSubscriber(object handler)
        {
            var subscriber = new RecordingSubscriber(handler);
            _subscribers.Enqueue(subscriber);
            return subscriber;
        }

        public void OnError(Exception exception) => Error = exception;
        public void Dispose() => Disposed = true;
    }

    internal sealed class RecordingSubscriber(object handler) : IMediatorSubscriberScope
    {
        public readonly object Handler = handler;
        public bool Disposed;
        public Exception? Error;

        public void OnError(Exception exception) => Error = exception;
        public void Dispose() => Disposed = true;
    }
}
