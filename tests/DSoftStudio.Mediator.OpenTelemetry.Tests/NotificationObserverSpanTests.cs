// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

public sealed record ObserverPing : INotification;

public sealed class ObserverHandlerOne : INotificationHandler<ObserverPing>
{
    public Task Handle(ObserverPing notification, CancellationToken ct) => Task.CompletedTask;
}

public sealed class ObserverHandlerTwo : INotificationHandler<ObserverPing>
{
    /// <summary>What this handler saw as ambient while it ran, captured for the assertions below.</summary>
    public static Activity? SeenActivity;
    public static string? SeenBaggage;

    public Task Handle(ObserverPing notification, CancellationToken ct)
    {
        SeenActivity = Activity.Current;
        SeenBaggage = Activity.Current?.GetBaggageItem("tenant");
        return Task.CompletedTask;
    }
}

/// <summary>
/// The observation port must emit the spans the decorator emitted, because downstream tooling
/// classifies them by tag and by parent/child structure — never by span name.
/// </summary>
[Collection("OTel")]
public class NotificationObserverSpanTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddMediatorInstrumentation();
        services.PrecompileNotifications();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ThePublishEmitsOneEnvelopeAndOneSpanPerSubscriber()
    {
        using var collector = new ActivityCollector();
        using var provider = Build();

        await provider.GetRequiredService<IMediator>()
            .Publish(new ObserverPing(), TestContext.Current.CancellationToken);

        var mediatorSpans = collector.Activities
            .Where(a => a.GetTagItem("mediator.request.type") as string == typeof(ObserverPing).FullName)
            .ToArray();

        // The envelope is the one WITHOUT a handler type — that absence is the only thing telling a
        // consumer a publish row from a subscriber row.
        var envelope = mediatorSpans.Single(a => a.GetTagItem("mediator.handler.type") is null);
        var subscribers = mediatorSpans.Where(a => a.GetTagItem("mediator.handler.type") is not null).ToArray();

        envelope.GetTagItem("mediator.request.kind").ShouldBe("notification");
        subscribers.Length.ShouldBe(2);
        subscribers.ShouldAllBe(s => (string)s.GetTagItem("mediator.request.kind")! == "notification");

        // The CONCRETE subscriber, not a wrapper.
        subscribers.Select(s => (string)s.GetTagItem("mediator.handler.type")!).ShouldBe(
            new[] { typeof(ObserverHandlerOne).FullName!, typeof(ObserverHandlerTwo).FullName! },
            ignoreOrder: true);

        // A fan, not a chain: every subscriber hangs off the envelope, never off its predecessor.
        subscribers.ShouldAllBe(s => s.ParentSpanId == envelope.SpanId);
    }

    [Fact]
    public async Task TheSubscriberSpanIsAmbientForTheHandlerAndCarriesBaggage()
    {
        ObserverHandlerTwo.SeenActivity = null;
        ObserverHandlerTwo.SeenBaggage = null;

        using var collector = new ActivityCollector();
        using var provider = Build();

        using var caller = new Activity("caller").Start();
        caller.AddBaggage("tenant", "acme");

        await provider.GetRequiredService<IMediator>()
            .Publish(new ObserverPing(), TestContext.Current.CancellationToken);

        // I6: whatever the handler emits must nest under ITS subscriber span, or an imported trace
        // drops those dependency spans for having no mediator ancestor.
        ObserverHandlerTwo.SeenActivity.ShouldNotBeNull();
        ObserverHandlerTwo.SeenActivity!.GetTagItem("mediator.handler.type")
            .ShouldBe(typeof(ObserverHandlerTwo).FullName);

        // Anchoring ambiently rather than with an explicit ActivityContext is what keeps this alive.
        // With an explicit parent the subscriber starts a fresh baggage scope and this reads null.
        ObserverHandlerTwo.SeenBaggage.ShouldBe("acme");

        // I9: publishing does not disturb the caller.
        Activity.Current.ShouldBe(caller);
    }
}
