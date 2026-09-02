// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Notifications;

public sealed record MembershipPing : INotification;

/// <summary>Discovered by the generator, so it is in the generated dispatch table.</summary>
public sealed class VisibleMembershipHandler : INotificationHandler<MembershipPing>
{
    public static int CallCount;

    public Task Handle(MembershipPing notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Registering an <see cref="INotificationPublisher"/> changes WHICH handlers run, not just how.
/// <para>
/// The default path dispatches the table the generator built at compile time. A publisher is handed
/// whatever the container returns. A handler the generator cannot see — registered by hand against
/// the interface — is therefore invisible to the default path and invoked once a publisher exists.
/// </para>
/// </summary>
public class PublisherHandlerMembershipTests
{
    private static ServiceProvider Build(bool withPublisher)
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();

        // Hand-registered against the interface only. `file`-scoped, so handler discovery skips it
        // and it never reaches the generated table.
        services.AddSingleton<INotificationHandler<MembershipPing>>(new HandRegisteredMembershipHandler());

        if (withPublisher)
            services.AddSingleton<INotificationPublisher, SequentialNotificationPublisher>();

        services.PrecompileNotifications();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task DefaultPath_SkipsAHandlerTheGeneratorCannotSee()
    {
        VisibleMembershipHandler.CallCount = 0;
        HandRegisteredMembershipHandler.CallCount = 0;

        using var provider = Build(withPublisher: false);
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish(new MembershipPing(), TestContext.Current.CancellationToken);

        VisibleMembershipHandler.CallCount.ShouldBe(1);
        HandRegisteredMembershipHandler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task WithAPublisher_TheSameHandlerIsInvoked()
    {
        VisibleMembershipHandler.CallCount = 0;
        HandRegisteredMembershipHandler.CallCount = 0;

        using var provider = Build(withPublisher: true);
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish(new MembershipPing(), TestContext.Current.CancellationToken);

        VisibleMembershipHandler.CallCount.ShouldBe(1);
        HandRegisteredMembershipHandler.CallCount.ShouldBe(1);
    }
}

file sealed class HandRegisteredMembershipHandler : INotificationHandler<MembershipPing>
{
    public static int CallCount;

    public Task Handle(MembershipPing notification, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        return Task.CompletedTask;
    }
}
