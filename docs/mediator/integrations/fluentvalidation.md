---
layout: default
title: "FluentValidation - DSoftStudio.Mediator"
description: "Automatic request validation with FluentValidation."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

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

## Validating One Request Instead of All of Them

`AddMediatorFluentValidation()` registers the behavior as an **open** generic, so it joins the pipeline of every request — including the ones with no validator at all, which then have a chain built for them instead of being dispatched directly to their handler.

The generic overload registers it closed over one pair instead:

```csharp
services
    .AddMediatorFluentValidation<CreateUser, Guid>()
    .PrecompilePipelines();
```

This form is opt-in per request, and that cuts both ways: a request whose pair is not registered is **not validated even if a validator for it exists in DI**, and nothing warns. Prefer the open form when validators are discovered by assembly scanning, or when having a validator is the norm.

The two forms are alternatives, not layers. If the open registration is already present the closed call stands down, because a second descriptor would run every validator twice.

## Order Against Caching

Pipeline behaviors run in registration order, and when a request is both cached and validated that
order decides whether a cache hit is validated at all.

```csharp
// Validation outer: every dispatch is validated, hits included.
services.AddMediatorFluentValidation();
services.AddMediatorHybridCache();

// Caching outer: a hit returns before validation is ever reached.
services.AddMediatorHybridCache();
services.AddMediatorFluentValidation();
```

With caching registered first, the first dispatch populates the entry and every later one is served
from it — **without running the validators**. Nothing warns; the rules are simply skipped. If
validation is authorization, a permission check, or anything else that can change its answer between
two calls with the same key, register **validation before caching**.

An invalid request never populates the cache in either order: the handler does not run, so there is
no value to store, and the next dispatch is rejected again rather than served a cached failure.

## Behavior Summary

| Scenario | Result |
|---|---|
| No validators registered for request | Pass-through — handler executes normally |
| All validators pass | Handler executes normally |
| One or more validators fail | `MediatorValidationException` thrown, handler not invoked |
| Validator has DI dependencies | Fully supported — validators are resolved from the DI container |
| Registered open-generic | Every request in the application gets a pipeline chain |
| Registered per pair | Only that pair is validated; a validator for any other request never runs |
| Registered after caching | A cache hit is served without validating the request |

## See Also

- [Pipeline Behaviors](../features/pipeline-behaviors.md) — how behaviors wrap the handler pipeline
- [CQRS](../concepts/cqrs.md) — validate commands separately from queries
- [Registration Order](../getting-started/registration-order.md) — register `AddMediatorFluentValidation()` before `PrecompilePipelines()`
