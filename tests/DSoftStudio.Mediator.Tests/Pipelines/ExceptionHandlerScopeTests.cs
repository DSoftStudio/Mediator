// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Pipelines;

// Which stage of the pipeline should blow up on this dispatch.
public enum GuardStage
{
    None,
    Pre,
    PreAfterSuspending,
    Handler,
    Post,
}

public sealed record GuardScopePing(GuardStage Fail) : IRequest<int>;

public sealed class GuardScopeHandler : IRequestHandler<GuardScopePing, int>
{
    public int CallCount;

    public ValueTask<int> Handle(GuardScopePing request, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        if (request.Fail == GuardStage.Handler)
            throw new InvalidOperationException("handler failed");
        return new(42);
    }
}

public sealed class GuardScopePreProcessor : IRequestPreProcessor<GuardScopePing>
{
    public int CallCount;

    public ValueTask Process(GuardScopePing request, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);

        // Two shapes on purpose: a throw that escapes Process synchronously, and one that surfaces
        // as a faulted ValueTask after the pre-processor has already suspended. They take different
        // routes through the guard.
        if (request.Fail == GuardStage.Pre)
            throw new InvalidOperationException("pre-processor failed");

        if (request.Fail == GuardStage.PreAfterSuspending)
            return ThrowAfterYield();

        return ValueTask.CompletedTask;
    }

    private static async ValueTask ThrowAfterYield()
    {
        await Task.Yield();
        throw new InvalidOperationException("pre-processor failed after suspending");
    }
}

public sealed class GuardScopePostProcessor : IRequestPostProcessor<GuardScopePing, int>
{
    public int CallCount;
    public int LastResponse;

    public ValueTask Process(GuardScopePing request, int response, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        LastResponse = response;

        if (request.Fail == GuardStage.Post)
            throw new InvalidOperationException("post-processor failed");

        return ValueTask.CompletedTask;
    }
}

public sealed class GuardScopeExceptionHandler : IRequestExceptionHandler<GuardScopePing, int>
{
    public int CallCount;

    public ValueTask Handle(GuardScopePing request, Exception exception, RequestExceptionHandlerState<int> state, CancellationToken ct)
    {
        Interlocked.Increment(ref CallCount);
        state.SetHandled(-1);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Pins WHAT the exception-handler guard covers: the pre-processors, the behavior chain and the
/// terminal handler — but deliberately not the post-processors.
/// </summary>
public class ExceptionHandlerScopeTests
{
    private static (ServiceProvider Provider, GuardScopePreProcessor Pre, GuardScopePostProcessor Post, GuardScopeExceptionHandler Ex) Build()
    {
        var pre = new GuardScopePreProcessor();
        var post = new GuardScopePostProcessor();
        var ex = new GuardScopeExceptionHandler();

        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton<IRequestPreProcessor<GuardScopePing>>(pre);
        services.AddSingleton<IRequestPostProcessor<GuardScopePing, int>>(post);
        services.AddSingleton<IRequestExceptionHandler<GuardScopePing, int>>(ex);
        services.PrecompilePipelines();

        return (services.BuildServiceProvider(), pre, post, ex);
    }

    [Fact]
    public async Task NothingThrows_ExceptionHandlerNotInvoked()
    {
        var (provider, pre, post, ex) = Build();
        using (provider)
        {
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new GuardScopePing(GuardStage.None), TestContext.Current.CancellationToken);

            result.ShouldBe(42);
            pre.CallCount.ShouldBe(1);
            post.CallCount.ShouldBe(1);
            post.LastResponse.ShouldBe(42);
            ex.CallCount.ShouldBe(0);
        }
    }

    [Fact]
    public async Task PreProcessorThrows_IsCoveredByTheGuard()
    {
        var (provider, pre, post, ex) = Build();
        using (provider)
        {
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new GuardScopePing(GuardStage.Pre), TestContext.Current.CancellationToken);

            result.ShouldBe(-1);
            pre.CallCount.ShouldBe(1);
            ex.CallCount.ShouldBe(1);
        }
    }

    [Fact]
    public async Task PreProcessorThrowsAfterSuspending_IsCoveredByTheGuard()
    {
        var (provider, pre, post, ex) = Build();
        using (provider)
        {
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new GuardScopePing(GuardStage.PreAfterSuspending), TestContext.Current.CancellationToken);

            result.ShouldBe(-1);
            pre.CallCount.ShouldBe(1);
            ex.CallCount.ShouldBe(1);
        }
    }

    [Fact]
    public async Task HandlerThrowsAndIsSuppressed_PostProcessorsStillRunOnTheSubstitutedResponse()
    {
        var (provider, pre, post, ex) = Build();
        using (provider)
        {
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new GuardScopePing(GuardStage.Handler), TestContext.Current.CancellationToken);

            result.ShouldBe(-1);
            ex.CallCount.ShouldBe(1);

            // The post-processors sit OUTSIDE the guard, so a suppressed exception still reaches
            // them, carrying the response the exception handler substituted. Widening the guard to
            // wrap the whole method would silently end this without failing any other test.
            post.CallCount.ShouldBe(1);
            post.LastResponse.ShouldBe(-1);
        }
    }

    [Fact]
    public async Task PostProcessorThrows_IsNotCoveredByTheGuard_AndPropagates()
    {
        var (provider, pre, post, ex) = Build();
        using (provider)
        {
            var mediator = provider.GetRequiredService<IMediator>();

            Func<Task> act = () => mediator
                .Send(new GuardScopePing(GuardStage.Post), TestContext.Current.CancellationToken)
                .AsTask();

            // Deliberate exclusion: by the time a post-processor runs a response already exists, so
            // substituting a different one would leave the post-processors that already ran having
            // observed a response that is not the one returned.
            await Should.ThrowAsync<InvalidOperationException>(act);
            post.CallCount.ShouldBe(1);
            ex.CallCount.ShouldBe(0);
        }
    }
}
