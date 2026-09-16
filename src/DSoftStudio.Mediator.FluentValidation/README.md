![DSoftStudio Mediator](https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg)

# DSoftStudio.Mediator.FluentValidation

[![NuGet](https://img.shields.io/nuget/v/DSoftStudio.Mediator.FluentValidation.svg)](https://www.nuget.org/packages/DSoftStudio.Mediator.FluentValidation)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)

FluentValidation integration for [DSoftStudio.Mediator](https://www.nuget.org/packages/DSoftStudio.Mediator). One pipeline behavior resolves the `IValidator<TRequest>` instances registered in DI, runs them all before the handler, and throws once with every failure they found.

## Features

- **Runs ahead of the handler** — a failing request never reaches it
- **All validators, all failures** — several validators per request type are supported; their failures are aggregated into one exception
- **Structured errors** — `MediatorValidationException` carries `Failures` and `ErrorsByProperty`, ready to map onto `ValidationProblemDetails`
- **Validators come from DI** — constructor dependencies work like any other service
- **Pass-through when unvalidated** — a request with no registered validator costs one length check

## Installation

```shell
dotnet add package DSoftStudio.Mediator.FluentValidation
```

## Quick Start

Write a request, its handler and a standard FluentValidation validator:

```csharp
using DSoftStudio.Mediator.Abstractions;
using FluentValidation;

public record CreateUser(string Name, string Email) : ICommand<Guid>;

public sealed class CreateUserHandler : ICommandHandler<CreateUser, Guid>
{
    public ValueTask<Guid> Handle(CreateUser request, CancellationToken cancellationToken)
        => new(Guid.NewGuid());
}

public sealed class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
```

Register the behavior and the validator at startup:

```csharp
using DSoftStudio.Mediator;
using DSoftStudio.Mediator.FluentValidation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services
    .AddMediator()
    .RegisterMediatorHandlers();

services.AddTransient<IValidator<CreateUser>, CreateUserValidator>();

services
    .AddMediatorFluentValidation()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();
```


> The three `Precompile*` calls arm three separate dispatch tables. `PrecompilePipelines()` is the
> one this package needs, but stopping there leaves the rest of your application without
> notification or stream dispatch — and the two fail differently: `CreateStream` throws and names
> the missing call, while `Publish` silently reaches no handlers. `AddMediator(builder => { })`
> calls all three for you.

## Registering validators

The behavior asks the container for `IValidator<TRequest>`, so a validator must be registered **against that service type**. Registering only the concrete class — `services.AddTransient<CreateUserValidator>()` — leaves it invisible to the behavior: no error, no warning, and the request is simply not validated.

**Manual registration (AOT- and trimming-safe)**

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddTransient<IValidator<CreateUser>, CreateUserValidator>();
```

**Assembly scanning** — needs [FluentValidation.DependencyInjectionExtensions](https://www.nuget.org/packages/FluentValidation.DependencyInjectionExtensions):

```shell
dotnet add package FluentValidation.DependencyInjectionExtensions
```

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddValidatorsFromAssembly(typeof(CreateUserValidator).Assembly);
```

> **Note:** the scanning extension uses reflection, which rules it out for NativeAOT and trimming. Register by hand there.

## Registration order

`AddMediatorFluentValidation()` registers `ValidationBehavior<,>` as an *open* generic. `PrecompilePipelines()` is the step that closes every open-generic behavior over the discovered request/response pairs and builds the pipeline chains, so the call has to sit **after `RegisterMediatorHandlers()` and before `PrecompilePipelines()`**.

Get the order wrong and the call is a no-op: the behavior is never closed, no chain is built for it, and invalid requests reach their handlers exactly as if the package were not installed. Nothing throws.

The analyzer reports **DSOFT010** when it can see the mistake, which means both calls in the same method on the same service collection: the Program.cs shape. A registration made from another method or another assembly is beyond what any analyzer can order, so keep the calls in one chain where the order is visible.


If a request is both cached and validated, registration order decides whether a cache **hit** is
validated: whichever behavior is registered first is the outer one. Register validation before
caching, or every dispatch after the first is served from the entry with its rules skipped.

## Validating one request instead of all of them

`AddMediatorFluentValidation()` registers the behavior as an open generic, so it joins the pipeline of **every** request — including the ones that have no validator at all. Those requests do not pay for validation itself (the behavior returns on an empty validator array), but a chain now exists where none did, and a request with no pipeline is dispatched directly to its handler.

When validation covers a known handful of requests, register those pairs instead:

```csharp
services
    .AddMediatorFluentValidation<CreateUser, Guid>()
    .AddMediatorFluentValidation<TransferMoney, string>()
    .PrecompilePipelines()
    .PrecompileNotifications()
    .PrecompileStreams();
```

**This form is opt-in per request, and that cuts both ways.** A request whose pair is not registered here is not validated *even if a validator for it exists in DI* — nothing warns, because from the pipeline's point of view there is nothing to run. Prefer the open form when validators are discovered by assembly scanning, or when having a validator is the norm rather than the exception.

The two forms are alternatives, not layers. If the open one has already been registered, the closed call stands down: it would otherwise put the behavior in that chain twice and run every validator twice.

## Handling failures

When any validator reports failures the behavior throws `MediatorValidationException` and the handler is skipped. The exception exposes two views of the same failures:

- `Failures` — `IReadOnlyList<ValidationFailure>`, every failure from every validator
- `ErrorsByProperty` — `IReadOnlyDictionary<string, string[]>`, the same messages grouped by property name

```csharp
using DSoftStudio.Mediator.Abstractions;
using DSoftStudio.Mediator.FluentValidation;

public static class ErrorHandlingSample
{
    public static async Task RunAsync(ISender mediator)
    {
        try
        {
            await mediator.Send(new CreateUser("", "invalid"));
        }
        catch (MediatorValidationException ex)
        {
            foreach (var failure in ex.Failures)
                Console.WriteLine($"{failure.PropertyName}: {failure.ErrorMessage}");

            foreach (var (property, messages) in ex.ErrorsByProperty)
                Console.WriteLine($"{property} => {string.Join("; ", messages)}");
        }
    }
}
```

## Validators with dependencies

Validators are resolved from the container, so they can take constructor dependencies:

```csharp
using DSoftStudio.Mediator.Abstractions;
using FluentValidation;

public record TransferMoney(string From, string To, decimal Amount) : ICommand<string>;

public sealed class TransferMoneyHandler : ICommandHandler<TransferMoney, string>
{
    public ValueTask<string> Handle(TransferMoney request, CancellationToken cancellationToken)
        => new($"transferred:{request.Amount}");
}

public interface IBlockedAccountService
{
    bool IsBlocked(string account);
}

public sealed class TransferMoneyValidator : AbstractValidator<TransferMoney>
{
    public TransferMoneyValidator(IBlockedAccountService blocked)
    {
        RuleFor(x => x.From)
            .Must(account => !blocked.IsBlocked(account))
            .WithMessage("Source account is blocked.");
    }
}
```

Register the dependency with the lifetime it needs; the validator's own lifetime has to be compatible with it.

## What the behavior does not do

- **No validator discovery.** The package never scans for validators — nothing is validated until you register it.
- **No validation without a registered pair, under the closed form.** Registering `IValidator<T>` is enough with the open registration; with `AddMediatorFluentValidation<T, TResponse>()` the pair has to be registered too.
- **Requests only.** `ValidationBehavior` is an `IPipelineBehavior`, so notifications published through `IPublisher` and streams from `CreateStream` are not validated.
- **No result type.** Failures arrive as a thrown `MediatorValidationException`, not as a returned result object.

## Documentation

📖 [Full documentation](https://docs.dsoftstudio.com/mediator/integrations/fluentvalidation)

## License

[MIT License](https://github.com/DSoftStudio/Mediator/blob/main/LICENSE.md)
