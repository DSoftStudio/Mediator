// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.Processors.Application.Processors;

/// <summary>
/// Logs every response after the handler completes successfully.
/// Demonstrates <see cref="IRequestPostProcessor{TRequest, TResponse}"/> — simpler
/// than a full pipeline behavior when you only need an "after" hook.
/// Not invoked if the handler throws.
/// </summary>
public sealed class AuditPostProcessor<TRequest, TResponse>(
    ILogger<AuditPostProcessor<TRequest, TResponse>> logger)
    : IRequestPostProcessor<TRequest, TResponse>
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        logger.LogInformation("[Post] {Request} → {Response}", typeof(TRequest).Name, response);
        return ValueTask.CompletedTask;
    }
}
