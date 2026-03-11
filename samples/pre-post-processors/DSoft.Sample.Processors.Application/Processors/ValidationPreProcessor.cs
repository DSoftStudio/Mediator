// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.Logging;

namespace DSoft.Sample.Processors.Application.Processors;

/// <summary>
/// Validates command requests before the handler runs.
/// Throws <see cref="ArgumentException"/> to short-circuit the pipeline.
/// Only runs for commands (skips queries via <see cref="ICommand"/> marker).
/// </summary>
public sealed class ValidationPreProcessor<TRequest>(
    ILogger<ValidationPreProcessor<TRequest>> logger) : IRequestPreProcessor<TRequest>
{
    public ValueTask Process(TRequest request, CancellationToken cancellationToken)
    {
        if (request is not ICommand)
            return ValueTask.CompletedTask;

        logger.LogInformation("[Pre] Validating {Request}", typeof(TRequest).Name);

        // Example: validate all public string properties are not empty
        foreach (var prop in typeof(TRequest).GetProperties())
        {
            if (prop.PropertyType == typeof(string))
            {
                var value = (string?)prop.GetValue(request);
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException($"{prop.Name} cannot be empty.");
            }
        }

        return ValueTask.CompletedTask;
    }
}
