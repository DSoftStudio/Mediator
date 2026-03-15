# FluentValidation Integration

The companion package **`DSoftStudio.Mediator.FluentValidation`** provides automatic request validation via [FluentValidation](https://docs.fluentvalidation.net/) — a single pipeline behavior that resolves all `IValidator<TRequest>` instances from DI and short-circuits on failure.

```shell
dotnet add package DSoftStudio.Mediator.FluentValidation
```

## Registration

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddMediatorFluentValidation()    // ← registers ValidationBehavior<,>
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();

// Register your validators
services.AddTransient<IValidator<CreateUser>, CreateUserValidator>();
```

> **Note:** Register `AddMediatorFluentValidation()` before `PrecompilePipelines()` so the validation behavior is included in the precompiled pipeline chain.

## Define a Validator

Use standard FluentValidation rules — validators are resolved from DI and can have injected dependencies:

```csharp
public record CreateUser(string Name, string Email) : ICommand<Guid>;

public class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
```

## What Happens on Failure

When validation fails, a `MediatorValidationException` is thrown before the handler executes. The exception contains:

- **`Failures`** — `IReadOnlyList<ValidationFailure>` with all errors from all validators
- **`ErrorsByProperty`** — `IReadOnlyDictionary<string, string[]>` grouped by property name, ready for `ValidationProblemDetails`

```csharp
try
{
    await mediator.Send(new CreateUser("", "bad"));
}
catch (MediatorValidationException ex)
{
    // ex.Failures → [{ PropertyName: "Name", ... }, { PropertyName: "Email", ... }]
    // ex.ErrorsByProperty → { "Name": ["Name is required."], "Email": ["..."] }
}
```

## Multiple Validators per Request

Multiple validators for the same request type are supported — all are executed and their failures are aggregated:

```csharp
services.AddTransient<IValidator<TransferMoney>, TransferMoneyAccountValidator>();
services.AddTransient<IValidator<TransferMoney>, TransferMoneyAmountValidator>();
```

## Mapping to ProblemDetails

In ASP.NET Core, map `MediatorValidationException` to a 400 response:

```csharp
app.UseExceptionHandler(error => error.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    if (ex is MediatorValidationException validationEx)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new ValidationProblemDetails(
            validationEx.ErrorsByProperty.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value)));
    }
}));
```

## Behavior Summary

| Scenario | Result |
|---|---|
| No validators registered for request | Pass-through — handler executes normally |
| All validators pass | Handler executes normally |
| One or more validators fail | `MediatorValidationException` thrown, handler not invoked |
| Validator has DI dependencies | Fully supported — validators are resolved from the DI container |
