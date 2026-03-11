// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Security;

// ── Dedicated stream types to avoid static state collisions ───────

public record CancelStream : IStreamRequest<int>;

public sealed class CancelStreamHandler : IStreamRequestHandler<CancelStream, int>
{
    public async IAsyncEnumerable<int> Handle(
        CancelStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}

// ── Tests ─────────────────────────────────────────────────────────

public class StreamCancellationTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public StreamCancellationTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileNotifications()
            .PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task CreateStream_CancellationDuringEnumeration_Throws()
    {
        using var cts = new CancellationTokenSource();
        var values = new List<int>();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var v in _mediator.CreateStream(new CancelStream(), cts.Token))
            {
                values.Add(v);
                if (values.Count == 3)
                    cts.Cancel();
            }
        });

        values.Count.ShouldBeLessThanOrEqualTo(4);
        values.Take(3).ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task CreateStream_AlreadyCancelled_ThrowsImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var values = new List<int>();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var v in _mediator.CreateStream(new CancelStream(), cts.Token))
            {
                values.Add(v);
            }
        });

        values.ShouldBeEmpty();
    }
}
