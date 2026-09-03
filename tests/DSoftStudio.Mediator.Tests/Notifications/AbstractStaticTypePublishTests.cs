// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Notifications;

public sealed record PhantomPing : INotification;

public sealed class PhantomPingHandler : INotificationHandler<PhantomPing>
{
    public static int Calls;
    public Task Handle(PhantomPing notification, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
}

/// <summary>An abstract base with NO handler of its own.</summary>
public abstract record PhantomBase : INotification;

public sealed record PhantomDerived : PhantomBase;

public sealed class PhantomDerivedHandler : INotificationHandler<PhantomDerived>
{
    public static int Calls;
    public Task Handle(PhantomDerived notification, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
}

/// <summary>An abstract base that DOES have a handler of its own.</summary>
public abstract record HandledBase : INotification;

public sealed record HandledDerived : HandledBase;

public sealed class HandledBaseHandler : INotificationHandler<HandledBase>
{
    public static int Calls;
    public Task Handle(HandledBase notification, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publishing through a variable whose STATIC type is abstract — <c>INotification</c> itself, or an
/// abstract base — used to report a publish and invoke nothing.
/// <para>
/// The generator claimed the call site and emitted an interceptor routing to the dispatch table of
/// the static type, which by construction is empty for a type nobody handles. The call compiled, ran,
/// and produced a publish with no subscribers: a phantom that telemetry faithfully reported.
/// </para>
/// </summary>
public class AbstractStaticTypePublishTests
{
    private static ServiceProvider Build()
    {
        PhantomPingHandler.Calls = 0;
        PhantomDerivedHandler.Calls = 0;
        HandledBaseHandler.Calls = 0;

        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.PrecompileNotifications();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PublishingThroughINotificationReachesTheRealHandler()
    {
        using var provider = Build();
        INotification notification = new PhantomPing();

        await provider.GetRequiredService<IMediator>()
            .Publish(notification, TestContext.Current.CancellationToken);

        PhantomPingHandler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task PublishingThroughAnAbstractBaseWithNoHandlerOfItsOwnReachesTheRealHandler()
    {
        using var provider = Build();
        PhantomBase notification = new PhantomDerived();

        await provider.GetRequiredService<IMediator>()
            .Publish(notification, TestContext.Current.CancellationToken);

        PhantomDerivedHandler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task AnAbstractBaseThatHasItsOwnHandlerStillGoesToThatHandler()
    {
        using var provider = Build();
        HandledBase notification = new HandledDerived();

        await provider.GetRequiredService<IMediator>()
            .Publish(notification, TestContext.Current.CancellationToken);

        // The only case where re-routing by runtime type would CHANGE behaviour, so it must not.
        // Subscribing to a base type is a deliberate choice; the table check is what preserves it.
        HandledBaseHandler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task PublishingAConcreteTypeIsUnchanged()
    {
        using var provider = Build();

        await provider.GetRequiredService<IMediator>()
            .Publish(new PhantomPing(), TestContext.Current.CancellationToken);

        PhantomPingHandler.Calls.ShouldBe(1);
    }
}
