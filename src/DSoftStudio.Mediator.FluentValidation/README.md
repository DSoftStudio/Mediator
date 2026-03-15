![DSoftStudio Mediator](https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg)

# DSoftStudio.Mediator.FluentValidation

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.FluentValidation.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator.FluentValidation)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)

FluentValidation integration for [DSoftStudio.Mediator](https://www.nuget.org/packages/DSoftStudio.Mediator). Provides automatic request validation via a pipeline behavior that resolves `IValidator<T>` instances from DI and short-circuits on failure.

## Features

- **Automatic validation** — All requests with registered validators are validated before reaching the handler
- **Multiple validators** — Supports multiple validators per request type, all errors are aggregated
- **Structured errors** — `MediatorValidationException` with typed error details for easy API response mapping
- **Zero configuration** — Register once, validates everywhere

## Installation

```shell
dotnet add package DSoftStudio.Mediator.FluentValidation
```

## Quick Start

Define a validator:

```csharp
public class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).EmailAddress();
    }
}
```

Register at startup:

```csharp
services
    .AddMediator()
    .RegisterMediatorHandlers()
    .AddMediatorFluentValidation(typeof(Program).Assembly)
    .PrecompilePipelines();
```

Validation runs automatically — if validation fails, a `MediatorValidationException` is thrown before the handler executes.

## Error Handling

```csharp
try
{
    await mediator.Send(new CreateUser("", "invalid"));
}
catch (MediatorValidationException ex)
{
    // ex.Errors contains structured validation errors
    foreach (var error in ex.Errors)
        Console.WriteLine($"{error.PropertyName}: {error.ErrorMessage}");
}
```

## Documentation

📖 [Full documentation](https://docs.dsoftstudio.com/mediator/integrations/fluentvalidation)

## License

[MIT License](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
