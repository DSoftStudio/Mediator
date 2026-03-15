---
layout: default
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# ADR-0004: Runtime-Typed Send(object) Dispatch

## Status

**Released in v1.1.0**

## Context

In message bus / command queue architectures, the consumer deserializes a command from
the wire (JSON, Protobuf, etc.) and only has the runtime `Type` + an `object` reference.
The consumer needs to dispatch the command through the mediator pipeline **without knowing
`TResponse` at compile time**.

```csharp
// Producer
var command = new CreateUserCommand("e@mail.com");
queue.Push((JsonSerializer.Serialize(command), command.GetType()));

// Consumer — only has object + Type at runtime
(string raw, Type type) = queue.Pop();
var command = JsonSerializer.Deserialize(raw, type);
await mediator.Send(command!); // <- needs runtime-typed dispatch
```

Current state:
- `Send<TRequest, TResponse>(TRequest)` — requires both type params at compile time
- `Send(Ping request)` — generated typed extensions, compile-time only
- `Publish(object)` — **already supports runtime-typed dispatch** via `NotificationObjectDispatch`
  (compile-time generated `FrozenDictionary<Type, DispatchDelegate>`)

The asymmetry between `Publish(object)` (supported) and `Send(object)` (not supported)
blocks a common real-world pattern.

## Decision

Add a runtime-typed `Send(object)` overload that follows the exact same architecture
as `Publish(object)`:

1. **`RequestObjectDispatch`** — new static class with a `FrozenDictionary<Type, DispatchDelegate>`
   dispatch table, mirroring `NotificationObjectDispatch`.

2. **Source generator** — `MediatorPipelineGenerator` emits `RequestObjectDispatch.Register<TReq, TRes>()`
   for every discovered request type, with a delegate that casts `object` → `TRequest`,
   resolves the handler/pipeline, calls `Handle()`, and boxes the response to `object?`.

3. **`SenderObjectExtensions.Send(this ISender, object, CancellationToken)`** → `ValueTask<object?>`
   — **extension method** (not an interface method — see [Design constraint: overload resolution](#design-constraint-overload-resolution)).

4. **`PrecompilePipelines()`** — calls `RequestObjectDispatch.Freeze()` after all registrations.

### Design constraint: overload resolution

`Send(object)` is implemented as an **extension method** rather than an interface method.
This is a deliberate design choice driven by C# overload resolution rules:

- `ISender` has `Send<TRequest, TResponse>(TRequest)` with **two** generic type parameters.
  Unlike MediatR's `Send<TResponse>(IRequest<TResponse>)` (one param, inferable), `TResponse`
  **cannot be inferred** from the argument alone.
- The source generator creates typed extension methods (e.g. `Send(this ISender, Ping)`) to
  solve the inference problem.
- In C#, **instance methods always beat extension methods** in overload resolution. If
  `Send(object)` were on the interface, `mediator.Send(new Ping())` would resolve to
  `Send(object)` instead of the generated typed extension — breaking all existing call sites.
- As an extension method, `Send(this ISender, Ping)` (more specific) wins over
  `Send(this ISender, object)` (less specific) by normal overload resolution.

This differs from `Publish(object)` which *is* on `IPublisher` — there, `Publish<T>(T)`
has one type parameter that *can* be inferred from the argument, so the generic overload
always wins over `Publish(object)` without ambiguity.

### Additional design constraints

- **Zero impact on the existing hot path.** The `Send<TRequest, TResponse>()` path (static
  generic dispatch, interceptors, `HandlerCache`) is completely untouched. The new overload
  is a separate code path with its own dispatch table.

- **AOT-safe.** No `MakeGenericType`, no `Expression.Compile`, no reflection at runtime.
  The source generator emits concrete delegates per request type.

- **Boxing is acceptable.** The `TResponse` is boxed to `object?` in the dispatch delegate.
  This only affects the `Send(object)` path. In queue/bus scenarios, the deserialization
  cost (~μs) dwarfs the boxing cost (~1 ns, 16-24 B for value types).

## Consequences

### Positive

- Enables message bus / command queue dispatch without `dynamic` or reflection
- Symmetric API: `Publish(object)` for notifications, `Send(object)` for requests
- Same proven architecture as `NotificationObjectDispatch` — no new patterns to maintain
- AOT and trimming compatible
- Zero impact on existing `Send<TRequest, TResponse>()` performance

### Negative

- One additional `FrozenDictionary` in static memory (~bytes per registered request type)
- `TResponse` boxing on the `Send(object)` path for value type responses
- Slightly more generated code in `MediatorRegistry.g.cs`

### Neutral

- Callers who don't use `Send(object)` pay zero cost — the dispatch table is populated
  at startup regardless (same as `NotificationObjectDispatch`), but never queried

---

## Document History

| Date       | Version | Changes |
|------------|---------|---------|
| —          | Draft   | Initial ADR with design proposal |
| 2026-03-15 | v1.1.0  | Released with FrozenDictionary-based dispatch |
