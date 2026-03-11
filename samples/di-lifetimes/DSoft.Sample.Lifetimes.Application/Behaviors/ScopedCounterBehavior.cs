// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.Lifetimes.Application.Behaviors;

/// <summary>
/// A <b>scoped</b> pipeline behavior that tracks how many requests
/// have been processed within the current DI scope (HTTP request).
/// <para>
/// Demonstrates that scoped behaviors maintain state per request,
/// resetting on each new scope — useful for per-request counters,
/// correlation IDs, or unit-of-work patterns.
/// </para>
/// </summary>
public sealed class ScopedCounterBehavior<TRequest, TResponse>(
    ILogger<ScopedCounterBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private int _callCount;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        _callCount++;

        logger.LogInformation(
            "[ScopedCounter] Request #{Count} in this scope: {Request}",
            _callCount,
            typeof(TRequest).Name);

        return await next.Handle(request, cancellationToken);
    }
}
