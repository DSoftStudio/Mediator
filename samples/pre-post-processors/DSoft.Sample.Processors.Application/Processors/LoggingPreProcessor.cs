// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.Processors.Application.Processors;

/// <summary>
/// Logs every request before the handler runs.
/// Demonstrates <see cref="IRequestPreProcessor{TRequest}"/> — simpler than a full
/// pipeline behavior when you only need a "before" hook.
/// </summary>
public sealed class LoggingPreProcessor<TRequest>(
    ILogger<LoggingPreProcessor<TRequest>> logger) : IRequestPreProcessor<TRequest>
{
    public ValueTask Process(TRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("[Pre] {Request}", typeof(TRequest).Name);
        return ValueTask.CompletedTask;
    }
}
