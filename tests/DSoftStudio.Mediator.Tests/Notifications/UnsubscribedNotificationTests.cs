// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Notifications;

/// <summary>A notification nobody subscribes to — deliberately no handler anywhere.</summary>
public sealed record UnsubscribedPing : INotification;

public sealed class NotANotification;

/// <summary>
/// Publishing a notification with no subscribers behaved differently depending on how the call was
/// spelled: the generic overload was a no-op, the object overload threw.
/// <para>
/// Dispatch plans are built from HANDLERS, so a notification nobody subscribes to has no entry in the
/// table — which is the ordinary state of a domain event nothing listens for yet, not a fault. The
/// object route treated a missing entry as a wiring error and said so in its message, sending anyone
/// who hit it to debug a registration that was perfectly fine.
/// </para>
/// <para>
/// Shipped in 1.3.0 and reached from any loop that publishes as <c>object</c> — a domain-event
/// dispatcher or an outbox. Joins the serialized collection because one case has to put the
/// process-global dispatch table back into its unbuilt state.
/// </para>
/// </summary>
[Collection("AggressiveDispatch")]
public class UnsubscribedNotificationTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.PrecompileNotifications();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PublishingToNobodyAsObjectAgreesWithTheGenericForm()
    {
        using var provider = Build();
        var mediator = provider.GetRequiredService<IMediator>();
        var notification = new UnsubscribedPing();

        // The generic form has always been a no-op here.
        await mediator.Publish(notification, TestContext.Current.CancellationToken);

        // The same publish, spelled the other way, must do the same thing.
        await mediator.Publish((object)notification, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishingSomethingThatIsNotANotificationStillThrows()
    {
        using var provider = Build();
        var mediator = provider.GetRequiredService<IMediator>();

        // Not a divergence to close: the object overload is doing by hand the check the generic
        // overload gets from the compiler, and it is a genuine caller error.
        var error = await Should.ThrowAsync<ArgumentException>(
            () => mediator.Publish(new NotANotification(), TestContext.Current.CancellationToken));

        error.Message.ShouldContain(nameof(NotANotification));
    }

    [Fact]
    public async Task PublishingBeforeTheDispatchTableIsBuiltStillThrows()
    {
        using var provider = Build();
        var mediator = provider.GetRequiredService<IMediator>();

        NotificationObjectDispatch.ResetForTests();
        try
        {
            // The error the original throw was written to catch, and the one worth keeping:
            // PrecompileNotifications() never ran, so nothing is registered at all.
            var error = await Should.ThrowAsync<InvalidOperationException>(
                () => mediator.Publish((object)new UnsubscribedPing(), TestContext.Current.CancellationToken));

            error.Message.ShouldContain("PrecompileNotifications");
        }
        finally
        {
            // Process-global: hand it back to every other test in the run.
            NotificationObjectDispatch.Freeze();
        }
    }
}
