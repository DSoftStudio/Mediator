// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;

namespace Host.Application.Behaviors;

/// <summary>
/// A simple logging behavior that wraps every request.
/// Demonstrates how <see cref="IPipelineBehavior{TRequest, TResponse}"/> works
/// with the Abstractions-only project pattern — the behavior is defined here
/// (only Abstractions), discovered by generators in <c>Host</c>, and fully
/// mockable/testable in <c>Host.Tests</c>.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    Action<string> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        log($"[Pipeline] Handling {requestName}");

        var stopwatch = Stopwatch.StartNew();
        var response = await next.Handle(request, cancellationToken);
        stopwatch.Stop();

        log($"[Pipeline] Handled {requestName} in {stopwatch.ElapsedMilliseconds}ms");

        return response;
    }
}
