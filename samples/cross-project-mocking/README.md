# Cross-Project Mocking Sample

Demonstrates the **recommended 3-project architecture** for testability with DSoftStudio.Mediator.

## Project Structure

```
Host (Composition Root)
├── References: DSoftStudio.Mediator (with source generators)
├── References: Host.Application
├── Program.cs — DI setup + pipeline execution
│
Host.Application (Domain / Application Layer)
├── References: DSoftStudio.Mediator.Abstractions (interfaces only)
├── Behaviors/ — LoggingBehavior (open-generic pipeline behavior)
├── Commands/  — CreateOrderCommand + handler
├── Queries/   — GetOrderQuery + handler
├── Services/  — OrderService (depends on ISender)
│
Host.Tests (Unit Tests)
├── References: DSoftStudio.Mediator.Abstractions (interfaces only)
├── References: Host.Application
├── Uses: Moq to mock ISender
├── OrderServiceTests.cs — 6 test patterns (including pipeline behavior)
```

## Why This Works

1. **Source generators run only in `Host`** — they discover handlers from `Host.Application` via the `ReferencedAssemblyScanner` Phase 2 (type-based fallback).
2. **`Host.Application` stays clean** — no generators, no generated code, just interfaces and handlers.
3. **`Host.Tests` is fully mock-safe** — no generators means no interceptors, so `Mock<ISender>` works in Debug and Release.

## Running

```shell
# Run the Host console app
dotnet run --project Host

# Run the tests
dotnet test Host.Tests
```

## Key Rule for Mocking

Always use the explicit two-generic-parameter form in `Setup`/`Verify`:

```csharp
// ✅ Interface method — mockable
mock.Setup(x => x.Send<CreateOrderCommand, int>(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(1001);

// ❌ Generated extension — NOT mockable
// mock.Setup(x => x.Send(It.IsAny<CreateOrderCommand>(), ...))
```
