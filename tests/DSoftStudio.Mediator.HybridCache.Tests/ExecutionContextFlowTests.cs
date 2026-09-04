// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.HybridCache.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.HybridCache.Tests;

/// <summary>
/// Pins that the caller's <see cref="ExecutionContext"/> survives the trip through
/// <c>HybridCache.GetOrCreateAsync</c> on a cache miss.
/// <para>
/// HybridCache only takes its stampede-protected, thread-pool-dispatched path when the caller's
/// token <i>can</i> be cancelled; that dispatch captures no ExecutionContext, so the cancellable
/// case is the one that regresses and the <see cref="CancellationToken.None"/> case is the control.
/// </para>
/// </summary>
public class ExecutionContextFlowTests
{
    [Fact]
    public async Task Handler_sees_the_callers_ambient_state_with_a_cancellable_token()
    {
        var provider = TestServiceProvider.Build();
        var mediator = provider.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        cts.Token.CanBeCanceled.ShouldBeTrue("the defect only reproduces on a cancellable token");

        AmbientProbe.Current.Value = "tenant-42";

        var observed = await mediator.Send(new GetAmbient(Guid.NewGuid().ToString("N")), cts.Token);

        observed.ShouldBe("tenant-42");
    }

    [Fact]
    public async Task Handler_sees_the_callers_ambient_state_with_a_non_cancellable_token()
    {
        var provider = TestServiceProvider.Build();
        var mediator = provider.GetRequiredService<IMediator>();

        AmbientProbe.Current.Value = "tenant-7";

        var observed = await mediator.Send(new GetAmbient(Guid.NewGuid().ToString("N")), CancellationToken.None);

        observed.ShouldBe("tenant-7");
    }

    [Fact]
    public async Task A_cancelled_token_still_abandons_the_wait()
    {
        // Guards the fix against the tempting shortcut of passing CancellationToken.None to
        // GetOrCreateAsync: that would restore context flow by taking the inline path, at the cost
        // of the caller's ability to walk away from a slow factory.
        var provider = TestServiceProvider.Build();
        var mediator = provider.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await mediator.Send(new GetAmbient(Guid.NewGuid().ToString("N")), cts.Token));
    }
}
