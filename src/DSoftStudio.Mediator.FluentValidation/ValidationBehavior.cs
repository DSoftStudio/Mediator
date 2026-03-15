// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using FluentValidation;
using ValidationFailure = FluentValidation.Results.ValidationFailure;

namespace DSoftStudio.Mediator.FluentValidation;

/// <summary>
/// Pipeline behavior that runs all registered <see cref="IValidator{T}"/> instances
/// for <typeparamref name="TRequest"/> before the handler executes.
/// <para>
/// If any validator reports failures, a <see cref="MediatorValidationException"/> is
/// thrown and the handler is never invoked.
/// </para>
/// <para>
/// When no validators are registered for <typeparamref name="TRequest"/>, the behavior
/// is a no-op pass-through.
/// </para>
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IValidator<TRequest>[] _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators as IValidator<TRequest>[] ?? validators.ToArray();
    }

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Length == 0)
            return await next.Handle(request, cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var failures = new List<ValidationFailure>();

        for (int i = 0; i < _validators.Length; i++)
        {
            var result = await _validators[i].ValidateAsync(context, cancellationToken);

            if (!result.IsValid)
                failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new MediatorValidationException(failures);

        return await next.Handle(request, cancellationToken);
    }
}
