// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

// ── Shared result ─────────────────────────────────────────
public sealed record OrderResult(int OrderId, bool Saved);

// ── DSoftStudio ───────────────────────────────────────────
public sealed record CreateOrderCommand(string Product, int Quantity) : IRequest<OrderResult>;

// ── MediatR ───────────────────────────────────────────────
public sealed record CreateOrderCommandMediatR(string Product, int Quantity) : MediatR.IRequest<OrderResult>;

// ── DispatchR ─────────────────────────────────────────────
public sealed class CreateOrderCommandDispatchR : global::DispatchR.Abstractions.Send.IRequest<CreateOrderCommandDispatchR, ValueTask<OrderResult>>
{
    public string Product { get; init; } = "";
    public int Quantity { get; init; }
}

// ── martinothamar/Mediator (source-generated) ─────────────
public sealed record CreateOrderCommandMediatorSG(string Product, int Quantity) : global::Mediator.IRequest<OrderResult>;

// ── Fake async DB repository (shared across all libraries) ─
/// <summary>
/// Simulates a lightweight async I/O call (e.g. database save).
/// Uses Task.Yield() to force an async continuation without real latency,
/// so we measure framework overhead — not wallclock DB time.
/// </summary>
public sealed class FakeOrderRepository
{
    private int _nextId;

    public async ValueTask<OrderResult> SaveAsync(string product, int quantity, CancellationToken ct)
    {
        await Task.Yield(); // simulate async I/O hop
        var id = Interlocked.Increment(ref _nextId);
        return new OrderResult(id, true);
    }
}
