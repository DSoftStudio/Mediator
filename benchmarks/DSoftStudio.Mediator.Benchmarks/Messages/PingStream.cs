// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
namespace Benchmarks;

// ── DSoftStudio ───────────────────────────────────────────
public record PingStream : IStreamRequest<int>;

// ── MediatR ───────────────────────────────────────────────
public record PingStreamMediatR : MediatR.IStreamRequest<int>;

// ── DispatchR ─────────────────────────────────────────────
public sealed class PingStreamDispatchR : global::DispatchR.Abstractions.Stream.IStreamRequest<PingStreamDispatchR, int>;

// ── martinothamar/Mediator (source-generated) ─────────────
public sealed record PingStreamMediatorSG : global::Mediator.IStreamRequest<int>;
