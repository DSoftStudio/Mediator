// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Lifetimes.Application.Queries;

// ═══════════════════════════════════════════════════════════════════
// Transient handler — new instance per resolution (opt-in override)
// ═══════════════════════════════════════════════════════════════════

public record TransientQuery : IQuery<LifetimeInfo>;

/// <summary>
/// Auto-registered as Singleton (no constructor params), but overridden to
/// <b>Transient</b> in Program.cs to demonstrate per-call instance creation.
/// Each call to <c>Send</c> gets a new handler instance → <see cref="InstanceId"/> changes every time.
/// </summary>
public sealed class TransientQueryHandler : IQueryHandler<TransientQuery, LifetimeInfo>
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];

    public ValueTask<LifetimeInfo> Handle(TransientQuery request, CancellationToken cancellationToken)
        => new(new LifetimeInfo(
            nameof(TransientQueryHandler),
            InstanceId,
            "Transient"));
}
