// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.ModularMonolith.Module;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.ModularMonolith.Tests;

/// <summary>
/// Regression tests for the modular monolith internal-handler fix (DSOFT005).
/// 
/// The Module project contains:
///   - <see cref="GetWeatherQuery"/>         + <c>internal</c> GetWeatherQueryHandler
///   - <see cref="GetTemperatureQuery"/>     + <b>public</b> GetTemperatureQueryHandler
///   - <see cref="WeatherUpdatedNotification"/> + <c>internal</c> WeatherUpdatedNotificationHandler
///
/// The generator in THIS project (the "host") should:
///   ✅ Register the public handler (GetTemperatureQueryHandler)
///   ❌ Skip the internal handlers (GetWeatherQueryHandler, WeatherUpdatedNotificationHandler)
///   ⚠️ Emit DSOFT005 warnings for the skipped handlers
///
/// If the fix regresses, the build itself fails with CS0122 (WarningsAsErrors=CS0122).
/// These tests verify runtime behavior on top of the compile-time guard.
/// </summary>
public class ModularMonolithTests
{
    private readonly IServiceProvider _sp;

    public ModularMonolithTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();

        _sp = services.BuildServiceProvider();
    }

    /// <summary>
    /// Public handler in the Module IS registered by the host's generator.
    /// Resolving and executing it must succeed.
    /// </summary>
    [Fact]
    public async Task Public_Handler_From_Module_Is_Registered()
    {
        var mediator = _sp.GetRequiredService<IMediator>();

        var result = await mediator.Send<GetTemperatureQuery, int>(new GetTemperatureQuery());

        Assert.Equal(25, result);
    }

    /// <summary>
    /// Internal query handler in the Module is NOT registered.
    /// Attempting to resolve it throws because no handler exists.
    /// </summary>
    [Fact]
    public async Task Internal_QueryHandler_From_Module_Is_Not_Registered()
    {
        var mediator = _sp.GetRequiredService<IMediator>();

        // GetWeatherQueryHandler is internal — not registered from this project.
        // The mediator should not be able to resolve a handler for GetWeatherQuery.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send<GetWeatherQuery, string>(new GetWeatherQuery()).AsTask());
    }

    /// <summary>
    /// Internal notification handler in the Module is NOT registered.
    /// Publishing the notification should succeed (no handlers = no-op),
    /// not throw CS0122 at compile time or fail at runtime.
    /// </summary>
    [Fact]
    public async Task Internal_NotificationHandler_From_Module_Is_Not_Registered()
    {
        var mediator = _sp.GetRequiredService<IMediator>();

        // WeatherUpdatedNotificationHandler is internal — not registered.
        // Publishing should complete without error (zero handlers is valid).
        await mediator.Publish(new WeatherUpdatedNotification("Madrid"));
    }

    /// <summary>
    /// The generated <c>ValidateMediatorHandlers()</c> does not include
    /// internal handlers from external modules in its validation set.
    /// </summary>
    [Fact]
    public void ValidateMediatorHandlers_Does_Not_Throw()
    {
        // If internal handlers leaked into generated validation code,
        // this would throw or fail to compile.
        _sp.ValidateMediatorHandlers();
    }
}
