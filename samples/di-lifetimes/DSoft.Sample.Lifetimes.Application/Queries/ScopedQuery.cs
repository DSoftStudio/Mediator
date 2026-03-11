// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Lifetimes.Application.Queries;

// ═══════════════════════════════════════════════════════════════════
// Scoped handler — one instance per HTTP request / DI scope
// ═══════════════════════════════════════════════════════════════════

public record ScopedQuery : IQuery<LifetimeInfo>;

/// <summary>
/// Auto-registered as Singleton (no constructor params), but overridden to
/// <b>Scoped</b> in Program.cs to demonstrate per-scope instance sharing.
/// Same instance within one HTTP request / DI scope, different across requests.
/// Use for: handlers that share state with other scoped services (DbContext, UnitOfWork).
/// </summary>
public sealed class ScopedQueryHandler : IQueryHandler<ScopedQuery, LifetimeInfo>
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];

    public ValueTask<LifetimeInfo> Handle(ScopedQuery request, CancellationToken cancellationToken)
        => new(new LifetimeInfo(
            nameof(ScopedQueryHandler),
            InstanceId,
            "Scoped"));
}
