// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Mediator;

/// <summary>
/// Tests the source-generated typed extension methods that enable
/// <c>mediator.Send(new Ping())</c> without explicit generic arguments.
/// </summary>
public class TypedExtensionTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public TypedExtensionTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Send_WithTypeInference_ReturnsCorrectResult()
    {
        // Uses generated extension: Send(Ping) → Send<Ping, int>(Ping)
        var result = await _mediator.Send(new Ping());

        result.ShouldBe(42);
    }

    [Fact]
    public async Task Send_WithExplicitGenerics_ReturnsSameResult()
    {
        // Explicit generics (existing API)
        var explicit_ = await _mediator.Send(new Ping());

        // Type-inferred (generated extension)
        var inferred = await _mediator.Send(new Ping());

        explicit_.ShouldBe(inferred);
    }

    [Fact]
    public async Task CreateStream_WithTypeInference_ReturnsCorrectValues()
    {
        // Uses generated extension: CreateStream(PingStream) → CreateStream<PingStream, int>(PingStream)
        var values = new List<int>();
        await foreach (var v in _mediator.CreateStream(new PingStream()))
            values.Add(v);

        values.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task CreateStream_WithExplicitGenerics_ReturnsSameValues()
    {
        var explicitValues = new List<int>();
        await foreach (var v in _mediator.CreateStream(new PingStream()))
            explicitValues.Add(v);

        var inferredValues = new List<int>();
        await foreach (var v in _mediator.CreateStream(new PingStream()))
            inferredValues.Add(v);

        explicitValues.ShouldBe(inferredValues);
    }
}
