// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace Benchmarks;

// ── DSoftStudio ───────────────────────────────────────────
public record PingNotification : INotification;

// ── MediatR ───────────────────────────────────────────────
public record PingNotificationMediatR : MediatR.INotification;

// ── DispatchR ─────────────────────────────────────────────
public sealed class PingNotificationDispatchR : global::DispatchR.Abstractions.Notification.INotification;
