// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

// ── DSoftStudio ───────────────────────────────────────────
public record Ping : IRequest<int>;
public record PingWithPipeline : IRequest<int>;

// ── MediatR ───────────────────────────────────────────────
public record PingMediatR : MediatR.IRequest<int>;

// ── DispatchR ─────────────────────────────────────────────
public sealed class PingDispatchR : global::DispatchR.Abstractions.Send.IRequest<PingDispatchR, ValueTask<int>>;

// ── martinothamar/Mediator (source-generated) ─────────────
public sealed record PingMediatorSG : global::Mediator.IRequest<int>;
