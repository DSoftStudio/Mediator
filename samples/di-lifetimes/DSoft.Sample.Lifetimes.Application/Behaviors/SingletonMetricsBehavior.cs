// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.Lifetimes.Application.Behaviors;

/// <summary>
/// A <b>singleton</b> pipeline behavior that counts total requests
/// across the entire application lifetime.
/// <para>
/// Demonstrates that singleton behaviors share state globally —
/// useful for metrics, rate limiting, or application-wide counters.
/// ⚠️ Must be thread-safe (uses Interlocked for atomic increment).
/// </para>
/// </summary>
public sealed class SingletonMetricsBehavior<TRequest, TResponse>(
    ILogger<SingletonMetricsBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private int _totalCount;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var count = Interlocked.Increment(ref _totalCount);

        logger.LogInformation(
            "[GlobalMetrics] Total request #{Count}: {Request}",
            count,
            typeof(TRequest).Name);

        return await next.Handle(request, cancellationToken);
    }
}
