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

/// <summary>A notification with no handler anywhere — the "resolved zero" case.</summary>
public sealed record SilentPing : INotification;

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

    private static ServiceProvider Build(RecordingObserver? observer, INotificationPublisher? publisher = null, RecordingObserver? second = null)
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();

        if (observer is not null)
            services.AddSingleton<IMediatorNotificationObserver>(observer);

        if (second is not null)
            services.AddSingleton<IMediatorNotificationObserver>(second);

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
        observer.Scope.ResolvedCount.ShouldBe(2);
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
    public async Task TwoObserversBothSeeThePublish()
    {
        ResetTierState();
        var bridge = new RecordingObserver();
        var profiler = new RecordingObserver();

        using var provider = Build(bridge, second: profiler);
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        // Two adapters observing at once is the normal case -- a tracing bridge and a profiler -- and
        // neither can be asked to stand down for the other. Resolving a single observer used to drop
        // whichever registered second, without a word.
        foreach (var observer in new[] { bridge, profiler })
        {
            observer.Publishes.ShouldBe(1);
            observer.Scope!.Subscribers.Count.ShouldBe(2);
            // Forwarded by the composite: the second adapter must be told the count too, or it is
            // back to counting the subscribers that started.
            observer.Scope.ResolvedCount.ShouldBe(2);
            observer.Scope.Subscribers.ShouldAllBe(s => s.Disposed);
            observer.Scope.Disposed.ShouldBeTrue();
        }
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

    [Fact]
    public async Task TheSubscriberCountArrivesOnceBeforeTheFirstSubscriberStarts()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer);
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        // The number the port cannot deliver any other way. It cannot ride on BeginPublish: the
        // observation has to open BEFORE the handlers are resolved, or it stops covering the
        // resolution it exists to measure. Without this signal an adapter can only count the
        // subscribers that STARTED, which after a failure part-way through the fan-out is a
        // different number from how many there were.
        observer.Scope!.ResolvedCalls.ShouldBe(1);
        observer.Scope.ResolvedCount.ShouldBe(2);

        // Before the first BeginSubscriber, so an adapter can size its per-subscriber state once
        // instead of growing it as the fan-out arrives.
        observer.Scope.SubscribersAtResolve.ShouldBe(0);
    }

    [Fact]
    public async Task TheSubscriberCountArrivesOnThePublisherRouteToo()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer, new SequentialNotificationPublisher());
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        // Both observed routes or neither: an adapter cannot ask which one the application is on,
        // so a signal that only one route sends is a signal it cannot rely on.
        observer.Scope!.ResolvedCalls.ShouldBe(1);
        observer.Scope.ResolvedCount.ShouldBe(2);
        observer.Scope.SubscribersAtResolve.ShouldBe(0);
    }

    [Fact]
    public async Task PublishingToNobodyReportsZeroRatherThanStayingSilent()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer);
        await provider.GetRequiredService<IMediator>()
            .Publish(new SilentPing(), TestContext.Current.CancellationToken);

        // Zero is reported, not skipped. Staying silent here would leave an adapter unable to tell
        // "this publish resolved no subscribers" from "this version never tells me", and those two
        // want opposite treatment in a report.
        observer.Scope!.ResolvedCalls.ShouldBe(1);
        observer.Scope.ResolvedCount.ShouldBe(0);
        observer.Scope.Subscribers.ShouldBeEmpty();
        observer.Scope.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishingToNobodyThroughAPublisherAlsoReportsZero()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer, new SequentialNotificationPublisher());
        await provider.GetRequiredService<IMediator>()
            .Publish(new SilentPing(), TestContext.Current.CancellationToken);

        observer.Scope!.ResolvedCalls.ShouldBe(1);
        observer.Scope.ResolvedCount.ShouldBe(0);
    }

    [Fact]
    public async Task TheCountIsNotConcurrentEvenWhenTheSubscribersAre()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer, new ParallelNotificationPublisher());
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        // BeginSubscriber is concurrent here — the parallel publisher queues each handler to the
        // thread pool — and the count still is not. The recording double reads and writes its
        // fields with no synchronisation at all, which is the point: adapters were going to add
        // defensive interlocks around a call that never races.
        observer.Scope!.ResolvedCalls.ShouldBe(1);
        observer.Scope.ResolvedCount.ShouldBe(2);
        observer.Scope.SubscribersAtResolve.ShouldBe(0);
        observer.Scope.Subscribers.Count.ShouldBe(2);
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

        /// <summary>How many times the count arrived. The contract says exactly one.</summary>
        public int ResolvedCalls;

        /// <summary>The count reported; -1 when it was never reported at all.</summary>
        public int ResolvedCount = -1;

        /// <summary>
        /// Subscribers already begun when the count arrived. Anything but zero means it arrived
        /// after the fan-out had started, which is what the ordering half of the contract forbids.
        /// </summary>
        public int SubscribersAtResolve = -1;

        // Plain fields, no interlocks: the contract says this call is not concurrent with respect
        // to the scope, and an unsynchronised double is how that stays true rather than merely
        // being written down.
        public void OnSubscribersResolved(int count)
        {
            SubscribersAtResolve = _subscribers.Count;
            ResolvedCount = count;
            ResolvedCalls++;
        }

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

    /// <summary>
    /// A null provider throws, naming the caller's parameter -- it does not resolve to "no observer".
    /// </summary>
    /// <remarks>
    /// The resolver used to carry a <c>if (serviceProvider is null) return null;</c> immediately
    /// after ArgumentNullException.ThrowIfNull, which could never run and stated the opposite
    /// contract to the line above it. Removing dead code is only safe once the live contract is
    /// written down, so here it is: null is a caller mistake, not an empty result.
    /// </remarks>
    [Fact]
    public void ResolveNotificationObserver_NullProvider_ThrowsNamingTheParameter()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => MediatorObservation.ResolveNotificationObserver(null!));

        // The point of the explicit guard: without it this surfaces as "provider" from inside
        // GetRequiredService, blaming a parameter the caller never saw.
        ex.ParamName.ShouldBe("serviceProvider");
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
