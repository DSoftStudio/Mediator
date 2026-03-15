// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

[Collection("OTel")]
public class TracingBehaviorTests
{
    [Fact]
    public async Task Command_creates_span_with_correct_name_and_kind()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorTracingBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        var result = await behavior.Handle(
            new TestCommand("test"), handler, CancellationToken.None);

        result.ShouldBe("handled:test");

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TestCommand command");
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task Query_creates_span_with_query_kind()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorTracingBehavior<TestQuery, string>(options);
        var handler = new TestQueryHandler();

        await behavior.Handle(new TestQuery(42), handler, CancellationToken.None);

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TestQuery query");
        activity.GetTagItem("mediator.request.kind")!.ShouldBe("query");
    }

    [Fact]
    public async Task Generic_request_creates_span_with_request_kind()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorTracingBehavior<TestRequest, int>(options);
        var handler = new TestRequestHandler();

        await behavior.Handle(new TestRequest("hello"), handler, CancellationToken.None);

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TestRequest request");
        activity.GetTagItem("mediator.request.kind")!.ShouldBe("request");
    }

    [Fact]
    public async Task Span_has_correct_tags()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorTracingBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        await behavior.Handle(new TestCommand("test"), handler, CancellationToken.None);

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.GetTagItem("mediator.request.type")!.ShouldBe(typeof(TestCommand).FullName);
        activity.GetTagItem("mediator.response.type")!.ShouldBe(typeof(string).FullName);
        activity.GetTagItem("mediator.request.kind")!.ShouldBe("command");
    }

    [Fact]
    public async Task Exception_sets_error_status_and_records_exception_event()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorTracingBehavior<FailingCommand, string>(options);
        var handler = new FailingCommandHandler();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FailingCommand("boom"), handler, CancellationToken.None));

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("boom");
        activity.GetTagItem("error.type")!.ShouldBe(typeof(InvalidOperationException).FullName);

        var exceptionEvent = activity.Events.ShouldHaveSingleItem();
        exceptionEvent.Name.ShouldBe("exception");
    }

    [Fact]
    public async Task Exception_event_includes_stacktrace_when_enabled()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions { RecordExceptionStackTraces = true };
        var behavior = new MediatorTracingBehavior<FailingCommand, string>(options);
        var handler = new FailingCommandHandler();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FailingCommand("boom"), handler, CancellationToken.None));

        var exceptionEvent = collector.Activities.Single().Events.ShouldHaveSingleItem();
        var stacktrace = exceptionEvent.Tags.FirstOrDefault(t => t.Key == "exception.stacktrace").Value;
        stacktrace.ShouldNotBeNull();
        ((string)stacktrace!).ShouldContain("InvalidOperationException");
    }

    [Fact]
    public async Task Exception_event_excludes_stacktrace_when_disabled()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions { RecordExceptionStackTraces = false };
        var behavior = new MediatorTracingBehavior<FailingCommand, string>(options);
        var handler = new FailingCommandHandler();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new FailingCommand("boom"), handler, CancellationToken.None));

        var exceptionEvent = collector.Activities.Single().Events.ShouldHaveSingleItem();
        var stacktrace = exceptionEvent.Tags.FirstOrDefault(t => t.Key == "exception.stacktrace").Value;
        stacktrace.ShouldBeNull();
    }

    [Fact]
    public async Task EnrichActivity_callback_adds_custom_tags()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions
        {
            EnrichActivity = (activity, request) =>
            {
                if (request is TestCommand cmd)
                    activity.SetTag("custom.value", cmd.Value);
            }
        };
        var behavior = new MediatorTracingBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        await behavior.Handle(new TestCommand("enriched"), handler, CancellationToken.None);

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.GetTagItem("custom.value")!.ShouldBe("enriched");
    }

    [Fact]
    public async Task No_span_when_no_listeners()
    {
        // No ActivityCollector → no listeners
        var options = new MediatorInstrumentationOptions();
        var behavior = new MediatorTracingBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        var result = await behavior.Handle(
            new TestCommand("test"), handler, CancellationToken.None);

        result.ShouldBe("handled:test");
        // No exception = pass-through works correctly
    }

    [Fact]
    public async Task No_span_when_tracing_disabled()
    {
        using var collector = new ActivityCollector();
        var options = new MediatorInstrumentationOptions { EnableTracing = false };
        var behavior = new MediatorTracingBehavior<TestCommand, string>(options);
        var handler = new TestCommandHandler();

        await behavior.Handle(new TestCommand("test"), handler, CancellationToken.None);

        collector.Activities.ShouldBeEmpty();
    }
}
