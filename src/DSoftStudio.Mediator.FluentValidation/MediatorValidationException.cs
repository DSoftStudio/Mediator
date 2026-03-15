// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using ValidationFailure = FluentValidation.Results.ValidationFailure;

namespace DSoftStudio.Mediator.FluentValidation;

/// <summary>
/// Thrown by <see cref="ValidationBehavior{TRequest,TResponse}"/> when one or more
/// FluentValidation validators report failures.
/// </summary>
public sealed class MediatorValidationException : Exception
{
    /// <summary>
    /// The validation failures that caused this exception.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }

    /// <summary>
    /// Validation failures grouped by property name.
    /// Useful for mapping to <c>ValidationProblemDetails.Errors</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> ErrorsByProperty { get; }

    public MediatorValidationException(IReadOnlyList<ValidationFailure> failures)
        : base(BuildMessage(failures))
    {
        Failures = failures;

        ErrorsByProperty = failures
            .GroupBy(f => f.PropertyName ?? string.Empty)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());
    }

    private static string BuildMessage(IReadOnlyList<ValidationFailure> failures)
    {
        if (failures.Count == 0)
            return "Validation failed.";

        if (failures.Count == 1)
            return $"Validation failed: {failures[0].ErrorMessage}";

        return $"Validation failed with {failures.Count} errors. First: {failures[0].ErrorMessage}";
    }
}
