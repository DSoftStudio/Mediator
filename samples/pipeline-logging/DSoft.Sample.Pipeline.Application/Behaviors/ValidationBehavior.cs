// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Pipeline.Application.Behaviors;

/// <summary>
/// Validates commands before they reach the handler.
/// Only runs for requests that implement <see cref="ICommand"/>.
/// Demonstrates conditional pipeline behavior using marker interfaces.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only validate commands, skip queries
         if (request is not ICommand)
            return next.Handle(request, cancellationToken);

        // Example: validate that command properties are not null/default
        var properties = typeof(TRequest).GetProperties();

        foreach (var prop in properties)
        {
            var value = prop.GetValue(request);

            if (value is string s && string.IsNullOrWhiteSpace(s))
                throw new ArgumentException($"{prop.Name} cannot be empty.");

            if (value is int i && i <= 0)
                throw new ArgumentException($"{prop.Name} must be greater than zero.");
        }

        return next.Handle(request, cancellationToken);
    }
}
