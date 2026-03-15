// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.SelfHandling.Application.Queries;

/// <summary>
/// Async self-handling query — demonstrates Task&lt;T&gt; return type.
/// The generator wraps the result in ValueTask&lt;T&gt; automatically.
/// </summary>
public record DelayedPingQuery(int Value) : IQuery<int>
{
    internal static async Task<int> Execute(DelayedPingQuery query, CancellationToken ct)
    {
        await Task.Delay(50, ct); // simulate async work
        return query.Value * 2;
    }
}
