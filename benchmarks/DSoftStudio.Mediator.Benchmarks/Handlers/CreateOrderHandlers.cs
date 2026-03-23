// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

// ── DSoftStudio ───────────────────────────────────────────
public sealed class CreateOrderHandler(FakeOrderRepository repo)
    : IRequestHandler<CreateOrderCommand, OrderResult>
{
    public ValueTask<OrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        => repo.SaveAsync(request.Product, request.Quantity, cancellationToken);
}

// ── MediatR ───────────────────────────────────────────────
public sealed class CreateOrderMediatRHandler(FakeOrderRepository repo)
    : MediatR.IRequestHandler<CreateOrderCommandMediatR, OrderResult>
{
    public async Task<OrderResult> Handle(CreateOrderCommandMediatR request, CancellationToken cancellationToken)
        => await repo.SaveAsync(request.Product, request.Quantity, cancellationToken);
}

// ── DispatchR ─────────────────────────────────────────────
public sealed class CreateOrderDispatchRHandler(FakeOrderRepository repo)
    : global::DispatchR.Abstractions.Send.IRequestHandler<CreateOrderCommandDispatchR, ValueTask<OrderResult>>
{
    public ValueTask<OrderResult> Handle(CreateOrderCommandDispatchR request, CancellationToken cancellationToken)
        => repo.SaveAsync(request.Product, request.Quantity, cancellationToken);
}

// ── martinothamar/Mediator (source-generated) ─────────────
public sealed class CreateOrderMediatorSGHandler(FakeOrderRepository repo)
    : global::Mediator.IRequestHandler<CreateOrderCommandMediatorSG, OrderResult>
{
    public async ValueTask<OrderResult> Handle(CreateOrderCommandMediatorSG request, CancellationToken cancellationToken)
        => await repo.SaveAsync(request.Product, request.Quantity, cancellationToken);
}
