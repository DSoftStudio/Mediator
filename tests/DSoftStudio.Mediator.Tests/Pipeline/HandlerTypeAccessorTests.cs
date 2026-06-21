// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.Tests.Pipeline;

// ADR-0049 — the pipeline chain exposes the CONCRETE terminal handler type to an outermost behavior via
// IPipelineHandlerTypeAccessor, so tracing/diagnostics can tag mediator.handler.type without resolving the
// handler. A behavior is open-generic / shared, so the right handler is only knowable by walking the chain.

public sealed class HandlerTypeAccessorTests
{
    public record AccReq : IRequest<int>;

    public sealed class AccReqHandler : IRequestHandler<AccReq, int>
    {
        public ValueTask<int> Handle(AccReq request, CancellationToken ct) => new(7);
    }

    // A passthrough behavior — stands for any number of cross-cutting links between the outermost behavior and
    // the handler. It is open to many handlers; only the chain knows which handler this request resolves to.
    public sealed class PassThroughBehavior : IPipelineBehavior<AccReq, int>
    {
        public ValueTask<int> Handle(AccReq request, IRequestHandler<AccReq, int> next, CancellationToken ct)
            => next.Handle(request, ct);
    }

    [Fact]
    public void Adapter_resolves_concrete_handler_type_through_a_single_link()
    {
        var handler = new AccReqHandler();
        IRequestHandler<AccReq, int> chain = new BehaviorHandlerAdapter<AccReq, int>(new PassThroughBehavior(), handler);

        var accessor = chain.ShouldBeAssignableTo<IPipelineHandlerTypeAccessor>();
        accessor.HandlerType.ShouldBe(typeof(AccReqHandler));
    }

    [Fact]
    public void Adapter_walks_the_full_chain_to_the_terminal_handler()
    {
        // adapter0 → adapter1 → adapter2 → handler. The outermost adapter must report the HANDLER, not the
        // inner adapters (which is the whole point — "look at the complete chain").
        var handler = new AccReqHandler();
        IRequestHandler<AccReq, int> chain = handler;
        for (int i = 0; i < 3; i++)
            chain = new BehaviorHandlerAdapter<AccReq, int>(new PassThroughBehavior(), chain);

        var accessor = chain.ShouldBeAssignableTo<IPipelineHandlerTypeAccessor>();
        accessor.HandlerType.ShouldBe(typeof(AccReqHandler));
    }

    // ── Stream side: StreamBehaviorHandlerAdapter exposes the same accessor (was 0% branch) ───────

    public record AccStreamReq : IStreamRequest<int>;

    public sealed class AccStreamHandler : IStreamRequestHandler<AccStreamReq, int>
    {
        public System.Collections.Generic.IAsyncEnumerable<int> Handle(AccStreamReq request, CancellationToken ct) => null!;
    }

    public sealed class PassThroughStreamBehavior : IStreamPipelineBehavior<AccStreamReq, int>
    {
        public System.Collections.Generic.IAsyncEnumerable<int> Handle(
            AccStreamReq request, IStreamRequestHandler<AccStreamReq, int> next, CancellationToken ct)
            => next.Handle(request, ct);
    }

    [Fact]
    public void Stream_adapter_walks_the_chain_to_the_terminal_handler()
    {
        var handler = new AccStreamHandler();
        IStreamRequestHandler<AccStreamReq, int> chain = handler;
        for (int i = 0; i < 3; i++)
            chain = new StreamBehaviorHandlerAdapter<AccStreamReq, int>(new PassThroughStreamBehavior(), chain);

        var accessor = chain.ShouldBeAssignableTo<IPipelineHandlerTypeAccessor>();
        accessor.HandlerType.ShouldBe(typeof(AccStreamHandler));
    }
}
