// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System.Diagnostics;
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.Pipeline.Application.Behaviors;

/// <summary>
/// Logs request name, elapsed time, and whether it succeeded or failed.
/// Demonstrates an open-generic pipeline behavior that applies to ALL requests.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation("[Pipeline] Handling {Request}", requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next.Handle(request, cancellationToken);
            stopwatch.Stop();

            logger.LogInformation(
                "[Pipeline] {Request} completed in {Elapsed}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(ex,
                "[Pipeline] {Request} failed after {Elapsed}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
