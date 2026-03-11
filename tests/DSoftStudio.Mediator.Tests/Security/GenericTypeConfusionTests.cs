// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

// ── Malicious request: same TResponse as Ping, but no handler ─────

public sealed class ConfusingPing : IRequest<int>;

// ── Tests ─────────────────────────────────────────────────────────

public class GenericTypeConfusionTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public GenericTypeConfusionTests()
    {
        var services = new ServiceCollection();
        services.AddMediator();

        // Register ONLY the real Ping handler + chain.
        services.AddTransient<IRequestHandler<Ping, int>, PingHandler>();
        services.AddScoped<PipelineChainHandler<Ping, int>>();

        // ConfusingPing has no handler and no chain — must fail.

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_ValidPing_ReturnsExpectedResult()
    {
        var result = await _mediator.Send(new Ping());

        result.ShouldBe(42);
    }

    [Fact]
    public async Task Send_ConfusingPing_FastPath_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _mediator.Send<ConfusingPing, int>(new ConfusingPing()));
    }

    [Fact]
    public async Task Send_ConfusingPing_DoesNotReturnPingResult()
    {
        // Ping works fine — returns 42.
        var validResult = await _mediator.Send(new Ping());
        validResult.ShouldBe(42);

        // ConfusingPing shares TResponse=int but must NEVER reuse Ping's pipeline.
        Func<Task> act = async () => await _mediator.Send<ConfusingPing, int>(new ConfusingPing());
        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
