// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class StreamTracingBehaviorTests
{
    [Fact]
    public async Task Stream_creates_span_with_correct_name_and_kind()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamTracingBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        var items = new List<int>();
        await foreach (var item in behavior.Handle(new TestStreamRequest(3), handler, CancellationToken.None))
        {
            items.Add(item);
        }

        items.ShouldBe([0, 1, 2]);

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TestStreamRequest stream");
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task Stream_span_has_correct_tags()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamTracingBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(1), handler, CancellationToken.None))
        { }

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.GetTagItem("mediator.request.type")!.ShouldBe(typeof(TestStreamRequest).FullName);
        activity.GetTagItem("mediator.response.type")!.ShouldBe(typeof(int).FullName);
        activity.GetTagItem("mediator.request.kind")!.ShouldBe("stream");
    }

    [Fact]
    public async Task Stream_span_covers_full_enumeration()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamTracingBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        // Enumeration should keep the span open until fully consumed
        await foreach (var _ in behavior.Handle(new TestStreamRequest(5), handler, CancellationToken.None))
        { }

        // Activity is stopped (added to collector) only after full enumeration
        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task Stream_error_during_enumeration_sets_error_status()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamTracingBehavior<FailingStreamRequest, int>(options);
        var handler = new FailingStreamHandler();

        var items = new List<int>();
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var item in behavior.Handle(new FailingStreamRequest(), handler, CancellationToken.None))
            {
                items.Add(item);
            }
        });

        // Should have received the first item before the error
        items.ShouldBe([1]);

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Stream_enrichment_callback_adds_custom_tags()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions
        {
            EnrichActivity = (activity, request) =>
            {
                if (request is TestStreamRequest sr)
                    activity.SetTag("custom.count", sr.Count);
            }
        };
        var behavior = new MediatorStreamTracingBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(2), handler, CancellationToken.None))
        { }

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.GetTagItem("custom.count")!.ShouldBe(2);
    }

    [Fact]
    public async Task No_span_when_no_listeners()
    {
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorStreamTracingBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        var items = new List<int>();
        await foreach (var item in behavior.Handle(new TestStreamRequest(3), handler, CancellationToken.None))
        {
            items.Add(item);
        }

        items.ShouldBe([0, 1, 2]);
    }

    [Fact]
    public async Task No_span_when_tracing_disabled()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions { EnableTracing = false };
        var behavior = new MediatorStreamTracingBehavior<TestStreamRequest, int>(options);
        var handler = new TestStreamHandler();

        await foreach (var _ in behavior.Handle(new TestStreamRequest(1), handler, CancellationToken.None))
        { }

        collector.Activities.ShouldBeEmpty();
    }
}
