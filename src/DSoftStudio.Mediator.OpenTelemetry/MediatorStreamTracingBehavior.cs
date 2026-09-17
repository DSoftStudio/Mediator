// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.OpenTelemetry;

/// <summary>
/// Stream pipeline behavior that creates distributed tracing spans for streamed requests.
/// The span covers the entire enumeration lifetime.
/// </summary>
public sealed class MediatorStreamTracingBehavior<TRequest, TResponse>(MediatorInstrumentationOptions options) : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private static readonly ActivitySource Source = MediatorInstrumentation.ActivitySource;

    public IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!options.EnableTracing || !Source.HasListeners())
            return next.Handle(request, cancellationToken);

        if (options.Filter is not null && !options.Filter(typeof(TRequest)))
            return next.Handle(request, cancellationToken);

        return Instrumented(request, next, cancellationToken);
    }

    // ── Why the enumerator is driven by hand ──────────────────────────
    //
    // C# forbids a catch clause in an iterator that contains a yield return, so the obvious shape
    // (an await foreach wrapped in try and catch) does not compile. The previous version settled for
    // try and finally, and paid for it three times over. It never saw the exception, so no span
    // carried an error type or an exception event. It could not tell a fault from a cancellation.
    // And its success flag was set only after the loop, so a consumer that simply stopped reading
    // (a break after the first page) disposed the iterator, ran the finally with success still false,
    // and painted the span red. A paged query reading its first rows reported as an outage.
    //
    // Driving MoveNextAsync inside its own try, and yielding OUTSIDE it, is the shape that gets a
    // catch back. The yield sits between the two, where no try encloses it.
    private async IAsyncEnumerable<TResponse> Instrumented(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity(
            MediatorStreamMetadata<TRequest, TResponse>.SpanName,
            ActivityKind.Internal);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("mediator.request.type", MediatorStreamMetadata<TRequest, TResponse>.RequestType);
            activity.SetTag("mediator.response.type", MediatorStreamMetadata<TRequest, TResponse>.ResponseType);
            activity.SetTag("mediator.request.kind", MediatorStreamMetadata<TRequest, TResponse>.RequestKind);
            // ADR-0049 — the concrete stream handler behind this request (resolved through the chain, never
            // instantiated), so an imported trace maps the stream span to its handler source. The request path
            // does the same in MediatorDispatchTracingObserver (streams have no dispatch port, so this stays a behavior).
            activity.SetTag("mediator.handler.type", ResolveHandlerType(next).FullName);

            options.EnrichActivity?.Invoke(activity, request);
        }

        // Per-item production metrics — measured here (the span already wraps the full enumeration) so an imported
        // trace can populate the profiler's STREAM TELEMETRY *production* block (items / TTFI / throughput), not
        // just lifecycle + duration. Stopwatch.GetTimestamp() math keeps this allocation-free and TFM-agnostic.
        long itemCount = 0;
        long startTimestamp = Stopwatch.GetTimestamp();
        long firstItemTimestamp = 0;

        // "early" until proven otherwise: if the consumer abandons the enumerator, none of the paths
        // below run again and the finally sees exactly this. Completion, cancellation and faults each
        // overwrite it on their way out.
        var termination = StreamTermination.Early;

        var enumerator = next.Handle(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool moved;

                // COST, measured: this pair of writes is 144 B and ~185 ns PER ITEM. Activity.Current is an
                // AsyncLocal, and writing one copies the ExecutionContext value map -- twice here, set and
                // restore. A thousand-item stream pays 144 KB and 185 us it did not pay before.
                //
                // Kept anyway, deliberately. The alternative is child spans that leave the stream subtree from
                // the second item on, which is not a degraded trace but a wrong one. The cost lands only when a
                // listener is attached -- Handle returns next.Handle directly when nothing is listening -- so an
                // application without OpenTelemetry pays none of it.
                //
                // If you are here to remove this: measure the parenting first. And note that no benchmark in this
                // repository covers the instrumented path, so a run will not tell you either way.
                //
                // Activity.Current is restored around every MoveNextAsync, not just the first.
                //
                // An async iterator only carries the ambient context it set while its own state
                // machine is running, and after the first `yield return` control has gone back to the
                // consumer -- so the second MoveNext resumed under the CONSUMER's Activity.Current and
                // the handler's child spans parented to the caller's span instead of this one.
                // Measured: item 0's dependency span parented here, item 1's parented to the caller.
                // A stream created under one span and enumerated after it ends came out as a root span
                // in a fresh trace, which is the ordinary shape of any API returning IAsyncEnumerable
                // to a framework that enumerates it later.
                var previous = Activity.Current;
                if (activity is not null)
                    Activity.Current = activity;

                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Distinct from a fault on purpose. A cancelled stream is usually the caller
                    // hanging up or a deadline firing, and burying it among exceptions makes a
                    // dashboard of stream errors unreadable.
                    termination = StreamTermination.Cancelled;
                    RecordFailure(activity, null);
                    throw;
                }
                catch (Exception ex)
                {
                    termination = StreamTermination.Faulted;
                    RecordFailure(activity, ex);
                    throw;
                }
                finally
                {
                    Activity.Current = previous;
                }

                if (!moved)
                {
                    termination = StreamTermination.Completed;
                    break;
                }

                if (itemCount == 0)
                    firstItemTimestamp = Stopwatch.GetTimestamp();
                itemCount++;

                // Outside every try above: this is what makes the catch clauses legal.
                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
            RecordTermination(activity, termination, itemCount, startTimestamp, firstItemTimestamp);
        }
    }

    /// <summary>
    /// Writes the production tags and the final status.
    /// </summary>
    /// <remarks>
    /// Extracted from the finally block rather than left inline. With the hand-driven loop above it,
    /// Instrumented reached a cognitive complexity of 22 against a limit of 15 -- and this half is a
    /// straight write-out that has nothing to do with driving the enumerator, so splitting there costs
    /// no context.
    /// </remarks>
    private static void RecordTermination(
        Activity? activity,
        StreamTermination termination,
        long itemCount,
        long startTimestamp,
        long firstItemTimestamp)
    {

            if (activity is { IsAllDataRequested: true })
            {
                double freq      = Stopwatch.Frequency;
                double elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / freq;

                activity.SetTag("mediator.stream.item_count", itemCount);
                activity.SetTag("mediator.stream.termination", TerminationName(termination));

                // Omitted rather than reported as zero when nothing was produced. Written
                // unconditionally, "first item in 0 ms" and "0 items per second" are indistinguishable
                // from measurements, and they entered downstream averages as if they were.
                if (itemCount > 0)
                {
                    activity.SetTag(
                        "mediator.stream.first_item_ms",
                        firstItemTimestamp > 0 ? (firstItemTimestamp - startTimestamp) * 1000.0 / freq : 0.0);

                    if (elapsedMs > 0)
                        activity.SetTag("mediator.stream.throughput_per_sec", itemCount * 1000.0 / elapsedMs);
                }
            }

            // Only a fault is an error. A consumer that stopped reading did nothing wrong, and a
            // cancellation is a normal end to a stream -- both used to arrive as Error with no
            // description, character-for-character identical to a crash.
            activity?.SetStatus(
                termination == StreamTermination.Faulted
                    ? ActivityStatusCode.Error
                    : ActivityStatusCode.Ok);
    }

    /// <summary>How the enumeration ended. Exported as <c>mediator.stream.termination</c>.</summary>
    private enum StreamTermination
    {
        /// <summary>The consumer stopped reading before the stream ran out — a `break`, or a disposal.</summary>
        Early,

        /// <summary>The stream ran to its end.</summary>
        Completed,

        /// <summary>The token was cancelled, or the handler observed cancellation.</summary>
        Cancelled,

        /// <summary>The handler threw.</summary>
        Faulted,
    }

    private static string TerminationName(StreamTermination termination) => termination switch
    {
        StreamTermination.Completed => "completed",
        StreamTermination.Cancelled => "cancelled",
        StreamTermination.Faulted => "faulted",
        _ => "early",
    };

    /// <summary>
    /// Tags the span for a failed enumeration. A cancellation passes <see langword="null"/>: it gets the
    /// error type for filtering but no exception event, because a stack trace for an expected stop is noise.
    /// </summary>
    private static void RecordFailure(Activity? activity, Exception? exception)
    {
        if (activity is not { IsAllDataRequested: true })
            return;

        activity.SetTag("error.type", (exception?.GetType() ?? typeof(OperationCanceledException)).FullName);

        if (exception is not null)
            activity.AddException(exception);
    }
    /// <summary>
    /// The concrete stream handler type at the end of the chain — via <see cref="IPipelineHandlerTypeAccessor"/>
    /// when <paramref name="next"/> is a chain adapter, or its runtime type when this behavior is the innermost link.
    /// </summary>
    private static Type ResolveHandlerType(IStreamRequestHandler<TRequest, TResponse> next)
        => next is IPipelineHandlerTypeAccessor accessor ? accessor.HandlerType : next.GetType();
}
