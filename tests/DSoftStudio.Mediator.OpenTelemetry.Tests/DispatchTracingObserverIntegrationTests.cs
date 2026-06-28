// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

// ── Recording processors (manually registered; the generator only registers handlers) ──

internal sealed class RecordingPreProcessor<TRequest>(List<string> log) : IRequestPreProcessor<TRequest>
{
    public ValueTask Process(TRequest request, CancellationToken ct)
    {
        log.Add("pre");
        return ValueTask.CompletedTask;
    }
}

internal sealed class RecordingPostProcessor<TRequest, TResponse>(List<string> log) : IRequestPostProcessor<TRequest, TResponse>
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
    {
        log.Add("post");
        return ValueTask.CompletedTask;
    }
}

// A request type used ONLY by the tracing-only gap test, with NO pipeline components anywhere — so its
// process-global RequestDispatch flag reflects solely that test's registration (no behavior/metrics forcing
// the chain). The generator only builds a pipeline chain for a type when it sees a behavior/processor for it;
// a dispatch observer must ALSO force the chain, otherwise a handler-only request would never be traced.
public sealed record TracingOnlyPing(string Value) : ICommand<string>;

public sealed class TracingOnlyPingHandler : IRequestHandler<TracingOnlyPing, string>
{
    public ValueTask<string> Handle(TracingOnlyPing request, CancellationToken cancellationToken)
        => new($"traced:{request.Value}");
}

/// <summary>
/// End-to-end proof that a REAL dispatch — wired through <c>AddMediatorInstrumentation</c> and run by the real
/// mediator — opens the request span via the core's dispatch-observation port (so the bridge's observer is
/// picked up by the mediator's <c>IEnumerable&lt;IMediatorDispatchObserver&gt;</c> injection) and that the span
/// wraps the whole pipeline, including pre-/post-processors. The isolated pieces are covered by the observer
/// unit tests and the core wiring tests; this proves they compose on the live <c>Send</c> path.
/// </summary>
[Collection("OTel")]
public class DispatchTracingObserverIntegrationTests
{
    [Fact]
    public async Task Tracing_only_observes_a_handler_only_request_with_no_other_pipeline_components()
    {
        // Reset the process-global dispatch flags for this type so the test reflects ONLY this collection's
        // registration. Otherwise another test's metrics-on PrecompilePipelines (which closes the open-generic
        // metrics behavior for every type) would have already marked this type's chain — masking the gap. The
        // [Collection("OTel")] attribute serializes these tests, so nothing re-marks it between here and Send.
        ResetDispatchFlags<TracingOnlyPing, string>();

        // Tracing only (NO metrics → no open-generic behavior forcing the chain) + a request with no
        // behaviors/processors. The observer must still wrap it, or handler-only requests escape tracing.
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddMediatorInstrumentation(o => o.EnableMetrics = false);
        services.PrecompilePipelines();

        // After the reset, this type has NO scanned pipeline component (no behaviors/pre/post/exception — the
        // only open-generic present is the stream-tracing behavior, which the request chain does not scan), so
        // the dispatch flag is true SOLELY because a dispatch observer is registered. Without the generator
        // forcing a chain for observed handler-only types, this stays false and the request bypasses the chain
        // — and the observer — entirely.
        services.Any(s => s.ServiceType == typeof(IPipelineBehavior<TracingOnlyPing, string>))
            .ShouldBeFalse("metrics are off → no open-generic behavior is closed for this type");
        global::DSoftStudio.Mediator.RequestDispatch<TracingOnlyPing, string>.HasPipelineChain
            .ShouldBeTrue("the dispatch observer must force a pipeline chain so the handler-only request is observed");

        using var collector = new ActivityCollector();
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IMediator>()
            .Send(new TracingOnlyPing("solo"), TestContext.Current.CancellationToken);

        result.ShouldBe("traced:solo");
        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TracingOnlyPing command");
    }

    // Test-only: clears the per-type process-global dispatch flags (no public reset exists — they are
    // write-once at startup) so a single test can observe its own registration in isolation.
    private static void ResetDispatchFlags<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        var type = typeof(global::DSoftStudio.Mediator.RequestDispatch<TRequest, TResponse>);
        foreach (var name in new[] { "_hasPipelineChain", "_isPipelineChainCacheable" })
        {
            type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .SetValue(null, false);
        }
    }

    [Fact]
    public async Task Live_send_opens_a_span_that_wraps_pre_and_post_processors()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddSingleton(log);
        services.AddSingleton<IRequestPreProcessor<TestCommand>>(_ => new RecordingPreProcessor<TestCommand>(log));
        services.AddSingleton<IRequestPostProcessor<TestCommand, string>>(_ => new RecordingPostProcessor<TestCommand, string>(log));
        services.AddMediatorInstrumentation();
        services.PrecompilePipelines();

        using var collector = new ActivityCollector();
        using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IMediator>()
            .Send(new TestCommand("e2e"), TestContext.Current.CancellationToken);

        result.ShouldBe("handled:e2e");

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TestCommand command");
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem("mediator.handler.type")!.ShouldBe(typeof(TestCommandHandler).FullName);

        // The pre- and post-processors ran as part of the same dispatch the span wraps (the exact begin→pre→
        // handler→post→dispose ordering is asserted with a fake observer in the core suite).
        log.ShouldBe(new[] { "pre", "post" });
    }

    [Fact]
    public async Task Live_send_records_error_status_when_the_handler_throws()
    {
        var services = new ServiceCollection();
        services.AddMediator().RegisterMediatorHandlers();
        services.AddMediatorInstrumentation();
        services.PrecompilePipelines();

        using var collector = new ActivityCollector();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await mediator.Send(new FailingCommand("boom"), TestContext.Current.CancellationToken));

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("boom");
        activity.GetTagItem("error.type")!.ShouldBe(typeof(InvalidOperationException).FullName);
        activity.Events.ShouldContain(e => e.Name == "exception");
    }
}
