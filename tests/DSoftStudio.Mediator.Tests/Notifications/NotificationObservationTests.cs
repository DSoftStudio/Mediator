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
    public async Task WithACustomPublisherTheObserverIsToldTheSubscribersAreUnobservable()
    {
        ResetTierState();
        var observer = new RecordingObserver();

        using var provider = Build(observer, new SequentialNotificationPublisher());
        await provider.GetRequiredService<IMediator>()
            .Publish(new ObservedPing(), TestContext.Current.CancellationToken);

        // The publisher owns the loop, so the core cannot reach the invocations. Saying so beats
        // emitting a publish with nothing under it, which reads as "no handlers ran".
        observer.Publishes.ShouldBe(1);
        observer.Scope!.SubscribersUnobservable.ShouldBeTrue();
        observer.Scope.Subscribers.ShouldBeEmpty();
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
        public readonly List<RecordingSubscriber> Subscribers = [];
        public bool SubscribersUnobservable;
        public bool Disposed;
        public Exception? Error;

        public IMediatorSubscriberScope? BeginSubscriber(object handler)
        {
            var subscriber = new RecordingSubscriber(handler);
            Subscribers.Add(subscriber);
            return subscriber;
        }

        public void OnSubscribersUnobservable() => SubscribersUnobservable = true;
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
