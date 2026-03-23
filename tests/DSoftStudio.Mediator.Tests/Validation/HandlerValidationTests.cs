// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using DSoftStudio.Mediator.Tests.SelfHandler;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Validation;

/// <summary>
/// Tests for the source-generated <c>ValidateMediatorHandlers()</c> fail-fast API.
/// Ensures that handler misconfiguration is detected at startup — not at first request.
/// </summary>
public class HandlerValidationTests
{
    [Fact]
    public void ValidateMediatorHandlers_AllRegistered_DoesNotThrow()
    {
        // Arrange — full registration (normal startup path)
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        // Register external dependencies required by test handlers
        // (Counter for CountedNotificationHandler1/2, List<string> for OrderedNotificationHandlerA/B)
        services.AddSingleton<Counter>();
        services.AddSingleton(new List<string>());
        services.AddSingleton<Greeter>();

        // Dependencies required by enterprise integration test handlers
        services.AddSingleton<Integration.SingletonCounter>();
        services.AddScoped<Integration.ScopedCorrelation>();
        services.AddTransient<Integration.TransientStamp>();
        services.AddSingleton(new ConcurrentBag<string>());
        services.AddSingleton(new ConcurrentBag<int>());
        services.AddSingleton(new Integration.FlakeyState(0));
        services.AddSingleton<Integration.IChaosRandom>(new Integration.ThreadSafeChaosRandom());
        services.AddSingleton(new Integration.ChaosConfig());

        using var provider = services.BuildServiceProvider();

        // Act & Assert — should not throw
        provider.ValidateMediatorHandlers();
    }

    [Fact]
    public void ValidateMediatorHandlers_MissingHandlers_ThrowsAggregateException()
    {
        // Arrange — mediator registered but NO handlers
        var services = new ServiceCollection();
        services.AddMediator();
        // Deliberately NOT calling RegisterMediatorHandlers()

        using var provider = services.BuildServiceProvider();

        // Act & Assert — should fail because handlers are not in DI
        var ex = Should.Throw<AggregateException>(() => provider.ValidateMediatorHandlers());
        ex.InnerExceptions.ShouldNotBeEmpty();
    }

    [Fact]
    public void ValidateMediatorHandlers_AggregateContainsAllFailures()
    {
        // Arrange — mediator registered but NO handlers
        var services = new ServiceCollection();
        services.AddMediator();

        using var provider = services.BuildServiceProvider();

        // Act
        var ex = Should.Throw<AggregateException>(() => provider.ValidateMediatorHandlers());

        // Assert — should contain multiple inner exceptions (one per missing handler type)
        ex.InnerExceptions.Count.ShouldBeGreaterThan(1);
        ex.Message.ShouldContain("mediator handlers failed validation");
    }
}
