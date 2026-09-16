// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

/// <summary>
/// How a stream ENDED, and what the span says about it.
/// <para>
/// Every case here came from a report with a reproduction attached. The behavior used to wrap the
/// enumeration in try/finally — the only shape available inline, because C# forbids a catch clause in
/// an iterator containing <c>yield return</c> — and set a `success` flag after the loop. Four
/// consequences, all of them measured by the reporter before anything here existed: a consumer that
/// stopped reading was reported as a failure, no failing stream carried an error type or an exception
/// event, cancelling and crashing and breaking produced spans identical character for character, and
/// an empty stream reported a first-item time and a throughput of zero as though they were readings.
/// </para>
/// </summary>
[Collection("OTel")]
public class StreamTerminationTests
{
    private static MediatorStreamTracingBehavior<TestStreamRequest, int> Tracing()
        => new(new MediatorInstrumentationOptions());

    // ── 1 · A consumer that stops reading has not failed ──────────────

    [Fact]
    public async Task Breaking_out_early_is_not_an_error()
    {
        using var collector = new ActivityCollector();

        await foreach (var _ in Tracing().Handle(
            new TestStreamRequest(10), new TestStreamHandler(), TestContext.Current.CancellationToken))
        {
            break; // a paged query reading its first page
        }

        var activity = collector.Activities.ShouldHaveSingleItem();

        activity.Status.ShouldBe(ActivityStatusCode.Ok,
            "a consumer that stopped reading did nothing wrong; this used to be painted Error");
        activity.GetTagItem("mediator.stream.termination").ShouldBe("early");
        activity.GetTagItem("mediator.stream.item_count").ShouldBe(1L);
    }

    [Fact]
    public async Task Running_to_the_end_is_completed()
    {
        using var collector = new ActivityCollector();

        await foreach (var _ in Tracing().Handle(
            new TestStreamRequest(3), new TestStreamHandler(), TestContext.Current.CancellationToken))
        { }

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem("mediator.stream.termination").ShouldBe("completed");
    }

    // ── 2 · A failure says what failed ────────────────────────────────

    [Fact]
    public async Task A_faulted_stream_carries_the_error_type_and_an_exception_event()
    {
        using var collector = new ActivityCollector();
        var behavior = new MediatorStreamTracingBehavior<FailingStreamRequest, int>(
            new MediatorInstrumentationOptions());

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in behavior.Handle(
                new FailingStreamRequest(), new FailingStreamHandler(), TestContext.Current.CancellationToken))
            { }
        });

        var activity = collector.Activities.ShouldHaveSingleItem();

        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("mediator.stream.termination").ShouldBe("faulted");
        activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName,
            "without this the span said only Error, with no description and nothing to filter on");

        activity.Events.ShouldContain(
            e => e.Name == "exception",
            "the request side records the exception; the stream side never saw it at all");
    }

    // ── 3 · The three endings are distinguishable ─────────────────────

    [Fact]
    public async Task Cancelled_is_not_the_same_span_as_faulted()
    {
        using var collector = new ActivityCollector();
        using var cts = new CancellationTokenSource();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in Tracing().Handle(
                new TestStreamRequest(100), new CancellationAwareStreamHandler(), cts.Token))
            {
                await cts.CancelAsync();
            }
        });

        var activity = collector.Activities.ShouldHaveSingleItem();

        activity.GetTagItem("mediator.stream.termination").ShouldBe("cancelled");
        activity.Status.ShouldBe(ActivityStatusCode.Ok,
            "a cancelled stream is a normal end, not a fault an operator should be paged for");
        activity.Events.ShouldNotContain(
            e => e.Name == "exception",
            "a stack trace for an expected stop is noise");
    }

    [Fact]
    public async Task The_three_endings_do_not_collide()
    {
        // The reporter's actual complaint: cancel, break and crash produced identical spans, so
        // neither an operator nor a profiler could tell them apart. Asserting the three values are
        // distinct guards the property rather than the spelling.
        var seen = new List<object?>();

        foreach (var scenario in new[] { "early", "completed", "faulted" })
        {
            using var collector = new ActivityCollector();

            if (scenario == "faulted")
            {
                var failing = new MediatorStreamTracingBehavior<FailingStreamRequest, int>(
                    new MediatorInstrumentationOptions());
                await Should.ThrowAsync<InvalidOperationException>(async () =>
                {
                    await foreach (var _ in failing.Handle(
                        new FailingStreamRequest(), new FailingStreamHandler(), TestContext.Current.CancellationToken))
                    { }
                });
            }
            else
            {
                await foreach (var _ in Tracing().Handle(
                    new TestStreamRequest(5), new TestStreamHandler(), TestContext.Current.CancellationToken))
                {
                    if (scenario == "early") break;
                }
            }

            seen.Add(collector.Activities.ShouldHaveSingleItem().GetTagItem("mediator.stream.termination"));
        }

        seen.Distinct().Count().ShouldBe(3, "three different endings reported as " + string.Join("/", seen));
    }

    // ── 4 · Zero is not a measurement ─────────────────────────────────

    [Fact]
    public async Task An_empty_stream_omits_the_production_tags_instead_of_reporting_zero()
    {
        using var collector = new ActivityCollector();

        await foreach (var _ in Tracing().Handle(
            new TestStreamRequest(0), new TestStreamHandler(), TestContext.Current.CancellationToken))
        { }

        var activity = collector.Activities.ShouldHaveSingleItem();

        activity.GetTagItem("mediator.stream.item_count").ShouldBe(0L);

        activity.GetTagItem("mediator.stream.first_item_ms").ShouldBeNull(
            "written as 0.0 this is indistinguishable from a first item that arrived instantly, and it " +
            "entered downstream averages as if it were a reading");
        activity.GetTagItem("mediator.stream.throughput_per_sec").ShouldBeNull();
    }

    [Fact]
    public async Task A_non_empty_stream_still_reports_them()
    {
        // The other half: omitting on empty must not become omitting always.
        using var collector = new ActivityCollector();

        await foreach (var _ in Tracing().Handle(
            new TestStreamRequest(3), new TestStreamHandler(), TestContext.Current.CancellationToken))
        { }

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.GetTagItem("mediator.stream.first_item_ms").ShouldNotBeNull();
    }

    // ── 5 · The span stays ambient for every item, not just the first ──

    [Fact]
    public async Task Handler_spans_parent_to_the_stream_span_on_every_item()
    {
        // Activity.Current is an AsyncLocal, and after the first `yield return` the iterator resumes
        // under the CONSUMER's context. Measured before the fix: item 0's child span parented to the
        // stream span, item 1's parented to the caller's — so a dependency recorded mid-stream left
        // the stream's subtree, and a stream enumerated after its creating span ended came out as a
        // root span in a new trace.
        using var collector = new ActivityCollector();
        using var caller = MediatorInstrumentation.ActivitySource.StartActivity("caller");

        var handler = new SpanPerItemStreamHandler();

        await foreach (var _ in Tracing().Handle(
            new TestStreamRequest(3), handler, TestContext.Current.CancellationToken))
        { }

        var parents = handler.ParentIds;

        var stream = collector.Activities.Single(a => a.DisplayName.EndsWith("stream", StringComparison.Ordinal));

        parents.Count.ShouldBe(3);
        parents.Distinct().Count().ShouldBe(1,
            "the parent changed partway through the enumeration: " + string.Join(" | ", parents));
        parents[0].ShouldBe(stream.Id);
    }
}

// ── Fixtures, file-scoped so they stay out of handler discovery ───────

/// <summary>
/// Starts a span per item from INSIDE the handler — the position the report measured. A span started
/// in the consumer's loop body is a different question: by then the iterator has yielded and the
/// consumer's own context is ambient again, which is correct and not what this guards.
/// </summary>
file sealed class SpanPerItemStreamHandler : IStreamRequestHandler<TestStreamRequest, int>
{
    public List<string?> ParentIds { get; } = [];

    public async IAsyncEnumerable<int> Handle(
        TestStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
        {
            using (var child = MediatorInstrumentation.ActivitySource.StartActivity("per-item"))
                ParentIds.Add(child?.ParentId);

            yield return i;
            await Task.Yield();
        }
    }
}

/// <summary>
/// Observes the token. TestStreamHandler does not, so cancelling mid-enumeration left it yielding
/// happily and nothing ever threw — the first version of the cancellation test asserted against a
/// stream that had simply run to completion.
/// </summary>
file sealed class CancellationAwareStreamHandler : IStreamRequestHandler<TestStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        TestStreamRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}
