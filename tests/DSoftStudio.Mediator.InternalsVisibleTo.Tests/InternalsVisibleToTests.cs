// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.InternalsVisibleTo.Host;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.InternalsVisibleTo.Tests;

/// <summary>
/// End-to-end test that exercises both Host and Test handlers through DI.
/// If CS0436 is present, this file won't even compile (WarningsAsErrors=CS0436).
/// </summary>
public class InternalsVisibleToTests
{
    private readonly IServiceProvider _sp;

    public InternalsVisibleToTests()
    {
        var services = new ServiceCollection();

        // Register mediator runtime
        services.AddMediator();

        // Register handlers from THIS project (generated code)
        services.RegisterMediatorHandlers();

        _sp = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Host_Handler_Resolves_Through_DI()
    {
        // PingQueryHandler lives in the Host project
        var mediator = _sp.GetRequiredService<IMediator>();
        var result = await mediator.Send<PingQuery, string>(new PingQuery());
        Assert.Equal("Pong", result);
    }

    [Fact]
    public async Task Test_Handler_Resolves_Through_DI()
    {
        // EchoQueryHandler lives in THIS test project
        var mediator = _sp.GetRequiredService<IMediator>();
        var result = await mediator.Send<EchoQuery, string>(new EchoQuery("Hello"));
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Handler_Validator_Does_Not_Throw()
    {
        // ValidateMediatorHandlers is generated as extension on IServiceProvider.
        // If CS0436 was present, there'd be ambiguity between Host's and Test's version.
        _sp.ValidateMediatorHandlers();
    }

    [Fact]
    public async Task Host_Notification_Handler_Resolves()
    {
        var mediator = _sp.GetRequiredService<IMediator>();
        // Should not throw — notification handler from Host is registered
        await mediator.Publish(new PingNotification());
    }
}
