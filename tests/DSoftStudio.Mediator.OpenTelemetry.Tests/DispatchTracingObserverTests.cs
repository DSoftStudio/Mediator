// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests;

/// <summary>
/// Unit tests for <see cref="MediatorDispatchTracingObserver"/> — the adapter that opens ONE span per request
/// dispatch through the core's <c>IMediatorDispatchObserver</c> port. It replaces the old per-behavior
/// MediatorTracingBehavior; a behavior could only wrap the behavior chain, while this wraps the WHOLE pipeline
/// (pre-/post-processors included). That "the span nests every pipeline component" guarantee is proven in the
/// core suite (the mediator owns pre/post execution); here we verify the span content and lifecycle.
/// </summary>
[Collection("OTel")]
public class DispatchTracingObserverTests
{
    [Fact]
    public void Command_creates_span_with_correct_name_and_kind()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions());

        var scope = observer.BeginDispatch<TestCommand, string>(new TestCommand("test"), new TestCommandHandler());
        scope.ShouldNotBeNull();
        scope!.Dispose(); // success → Ok

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TestCommand command");
        activity.Kind.ShouldBe(ActivityKind.Internal);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public void Query_creates_span_with_query_kind()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions());

        var scope = observer.BeginDispatch<TestQuery, string>(new TestQuery(42), new TestQueryHandler());
        scope!.Dispose();

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TestQuery query");
        activity.GetTagItem("mediator.request.kind")!.ShouldBe("query");
    }

    [Fact]
    public void Generic_request_creates_span_with_request_kind()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions());

        var scope = observer.BeginDispatch<TestRequest, int>(new TestRequest("hello"), new TestRequestHandler());
        scope!.Dispose();

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("TestRequest request");
        activity.GetTagItem("mediator.request.kind")!.ShouldBe("request");
    }

    [Fact]
    public void Span_has_correct_tags()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions());

        var scope = observer.BeginDispatch<TestCommand, string>(new TestCommand("test"), new TestCommandHandler());
        scope!.Dispose();

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.GetTagItem("mediator.request.type")!.ShouldBe(typeof(TestCommand).FullName);
        activity.GetTagItem("mediator.response.type")!.ShouldBe(typeof(string).FullName);
        activity.GetTagItem("mediator.request.kind")!.ShouldBe("command");
    }

    [Fact]
    public void Span_tags_concrete_handler_type()
    {
        // ADR-0049 — the request span must carry the concrete handler type so an imported trace can map it to
        // its handler source (and anchor HTTP/DB child spans as dependencies under it). The mediator hands the
        // observer the terminal handler directly, so its runtime type IS the concrete type.
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions());

        var scope = observer.BeginDispatch<TestCommand, string>(new TestCommand("test"), new TestCommandHandler());
        scope!.Dispose();

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.GetTagItem("mediator.handler.type")!.ShouldBe(typeof(TestCommandHandler).FullName);
        // NOTE: the chain case (the handler is a multi-link adapter that resolves the terminal handler via
        // IPipelineHandlerTypeAccessor) is proven in the core suite — HandlerTypeAccessorTests — because the real
        // BehaviorHandlerAdapter is internal to DSoftStudio.Mediator.
    }

    [Fact]
    public void Error_sets_error_status_and_records_exception_event()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions());

        var scope = observer.BeginDispatch<FailingCommand, string>(new FailingCommand("boom"), new FailingCommandHandler());
        scope!.OnError(new InvalidOperationException("boom"));
        scope.Dispose();

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("boom");
        activity.GetTagItem("error.type")!.ShouldBe(typeof(InvalidOperationException).FullName);

        var exceptionEvent = activity.Events.ShouldHaveSingleItem();
        exceptionEvent.Name.ShouldBe("exception");
    }

    [Fact]
    public void Exception_event_includes_stacktrace_when_enabled()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(
            new MediatorInstrumentationOptions { RecordExceptionStackTraces = true });

        var scope = observer.BeginDispatch<FailingCommand, string>(new FailingCommand("boom"), new FailingCommandHandler());
        scope!.OnError(new InvalidOperationException("boom"));
        scope.Dispose();

        var exceptionEvent = collector.Activities.Single().Events.ShouldHaveSingleItem();
        var stacktrace = exceptionEvent.Tags.FirstOrDefault(t => t.Key == "exception.stacktrace").Value;
        stacktrace.ShouldNotBeNull();
        ((string)stacktrace!).ShouldContain("InvalidOperationException");
    }

    [Fact]
    public void Exception_event_excludes_stacktrace_when_disabled()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(
            new MediatorInstrumentationOptions { RecordExceptionStackTraces = false });

        var scope = observer.BeginDispatch<FailingCommand, string>(new FailingCommand("boom"), new FailingCommandHandler());
        scope!.OnError(new InvalidOperationException("boom"));
        scope.Dispose();

        var exceptionEvent = collector.Activities.Single().Events.ShouldHaveSingleItem();
        var stacktrace = exceptionEvent.Tags.FirstOrDefault(t => t.Key == "exception.stacktrace").Value;
        stacktrace.ShouldBeNull();
    }

    [Fact]
    public void EnrichActivity_callback_adds_custom_tags()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions
        {
            EnrichActivity = (activity, request) =>
            {
                if (request is TestCommand cmd)
                    activity.SetTag("custom.value", cmd.Value);
            }
        });

        var scope = observer.BeginDispatch<TestCommand, string>(new TestCommand("enriched"), new TestCommandHandler());
        scope!.Dispose();

        var activity = collector.Activities.ShouldHaveSingleItem();
        activity.GetTagItem("custom.value")!.ShouldBe("enriched");
    }

    [Fact]
    public void IsActive_true_when_listener_attached()
    {
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions());

        observer.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Not_active_and_no_span_when_no_listeners()
    {
        // No ActivityCollector → no listener on our source. The core gates on IsActive (so BeginDispatch is
        // never called); even if it were, StartActivity returns null and no span is created.
        var observer = new MediatorDispatchTracingObserver(new MediatorInstrumentationOptions());

        observer.IsActive.ShouldBeFalse();
        observer.BeginDispatch<TestCommand, string>(new TestCommand("test"), new TestCommandHandler()).ShouldBeNull();
    }

    [Fact]
    public void Not_active_when_tracing_disabled()
    {
        // Even with a listener attached, disabling tracing makes the observer inactive, so the core never wraps
        // the dispatch.
        using var collector = new ActivityCollector();
        var observer = new MediatorDispatchTracingObserver(
            new MediatorInstrumentationOptions { EnableTracing = false });

        observer.IsActive.ShouldBeFalse();
    }
}
