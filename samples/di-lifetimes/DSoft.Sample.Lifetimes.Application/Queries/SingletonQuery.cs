// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Lifetimes.Application.Queries;

// ═══════════════════════════════════════════════════════════════════
// Singleton handler — one instance for the lifetime of the application
// ═══════════════════════════════════════════════════════════════════

public record SingletonQuery : IQuery<LifetimeInfo>;

/// <summary>
/// Registered as <b>Singleton</b> (auto-detected — no constructor params).
/// Same instance for the entire application lifetime → <see cref="InstanceId"/> never changes.
/// This is the default for stateless handlers — no override needed.
/// ⚠️ Must be thread-safe. Cannot depend on scoped services (e.g., DbContext).
/// </summary>
public sealed class SingletonQueryHandler : IQueryHandler<SingletonQuery, LifetimeInfo>
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];

    public ValueTask<LifetimeInfo> Handle(SingletonQuery request, CancellationToken cancellationToken)
        => new(new LifetimeInfo(
            nameof(SingletonQueryHandler),
            InstanceId,
            "Singleton"));
}
