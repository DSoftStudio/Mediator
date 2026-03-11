// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.Tests.Infrastructure;

// ── Request messages ──────────────────────────────────────────────

public record Ping : IRequest<int>;

public record PingVoid : IRequest<Unit>;

// Use a unique type so legacy-path tests never collide with fast-path static state.
public record LegacyPing : IRequest<int>;

public record LegacyPingVoid : IRequest<Unit>;

// Async request whose handler completes asynchronously (not synchronously).
public record SlowPing : IRequest<int>;

// ── Notification messages ─────────────────────────────────────────

public record PingNotification : INotification;

public record OrderedNotification : INotification;

public record UnregisteredNotification : INotification;

// ── Stream messages ───────────────────────────────────────────────

public record PingStream : IStreamRequest<int>;

// Dedicated type for StreamBehaviorTests to avoid static pipeline race conditions.
public record BehaviorPingStream : IStreamRequest<int>;
