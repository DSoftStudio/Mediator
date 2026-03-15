// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.SelfHandler;

// ── Self-handling request types ───────────────────────────────────

/// <summary>
/// A simple DI service injected into self-handlers.
/// </summary>
public sealed class Greeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

/// <summary>
/// Sync self-handler: static T Execute(...) pattern.
/// </summary>
public record SelfHandledPing(int Value) : ICommand<int>
{
    internal static int Execute(SelfHandledPing cmd)
    {
        return cmd.Value * 2;
    }
}

/// <summary>
/// Self-handler with a DI service parameter.
/// </summary>
public record SelfHandledGreet(string Name) : IQuery<string>
{
    internal static string Execute(SelfHandledGreet query, Greeter greeter)
    {
        return greeter.Greet(query.Name);
    }
}

/// <summary>
/// Async self-handler: static Task&lt;T&gt; Execute(...) pattern.
/// </summary>
public record SelfHandledAsync(int Value) : ICommand<int>
{
    internal static async Task<int> Execute(SelfHandledAsync cmd, CancellationToken ct)
    {
        await Task.Yield();
        return cmd.Value + 10;
    }
}

/// <summary>
/// Void self-handler: static void Execute(...) pattern (ICommand&lt;Unit&gt;).
/// </summary>
public record SelfHandledVoid : ICommand<Unit>
{
    public static bool WasExecuted;

    internal static void Execute(SelfHandledVoid cmd)
    {
        WasExecuted = true;
    }
}

// ── Tests ─────────────────────────────────────────────────────────

public class SelfHandlerTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public SelfHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Greeter>();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task SelfHandler_Sync_ReturnsCorrectValue()
    {
        var result = await _mediator.Send<SelfHandledPing, int>(
            new SelfHandledPing(21));

        result.ShouldBe(42);
    }

    [Fact]
    public async Task SelfHandler_WithDIService_ReturnsCorrectValue()
    {
        var result = await _mediator.Send<SelfHandledGreet, string>(
            new SelfHandledGreet("World"));

        result.ShouldBe("Hello, World!");
    }

    [Fact]
    public async Task SelfHandler_Async_ReturnsCorrectValue()
    {
        var result = await _mediator.Send<SelfHandledAsync, int>(
            new SelfHandledAsync(32));

        result.ShouldBe(42);
    }

    [Fact]
    public async Task SelfHandler_Void_ExecutesSuccessfully()
    {
        SelfHandledVoid.WasExecuted = false;

        var result = await _mediator.Send<SelfHandledVoid, Unit>(
            new SelfHandledVoid());

        result.ShouldBe(Unit.Value);
        SelfHandledVoid.WasExecuted.ShouldBeTrue();
    }
}
