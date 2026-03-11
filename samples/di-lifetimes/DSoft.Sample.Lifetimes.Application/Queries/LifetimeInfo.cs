// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Lifetimes.Application.Queries;

// ── Response DTO ──────────────────────────────────────────────────

/// <summary>
/// Returns the handler's instance ID so the caller can verify
/// whether the same instance was reused across requests.
/// </summary>
public record LifetimeInfo(string HandlerType, string InstanceId, string Lifetime);
