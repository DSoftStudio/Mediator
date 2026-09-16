---
layout: default
title: "Runtime Dispatch - DSoftStudio.Mediator"
description: "Send(object) and Publish(object) with static dispatch tables."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Runtime-Typed Dispatch (`Send(object)`)

For message bus, command queue, and event sourcing scenarios where the consumer deserializes a request from the wire and only has an `object` reference at runtime:

```csharp
// Producer — serialize command + type
var command = new CreateUser("alice@example.com");
queue.Push((JsonSerializer.Serialize(command), command.GetType().AssemblyQualifiedName));

// Consumer — deserialize and dispatch without knowing TResponse
(string raw, string typeName) = queue.Pop();
var type = Type.GetType(typeName)!;
var command = JsonSerializer.Deserialize(raw, type)!;

var result = await mediator.Send(command); // runtime-typed dispatch → ValueTask<object?>
```

The `Send(object)` overload compiles to a generated type switch over every request type the compilation can see. No reflection, no `MakeGenericType` — the dispatch is ordinary C# the compiler checks.

## How It Works

- The source generator emits a `switch (request)` with one type-pattern case per request type it discovered
- Each case calls straight into that pair's dispatch — the same chain the typed `Send<TRequest, TResponse>()` uses, so both routes share one cached handler and one pipeline chain
- The response is boxed as `object?` on the way out. Boxing happens only on this path; typed `Send` stays allocation-free
- A request type the compilation could not see — arriving from an assembly compiled without the generator — falls through to a `FrozenDictionary<Type, DispatchDelegate>` populated at registration. Still no reflection, one dictionary lookup slower

Each case body lives in its own method rather than inside the switch. That is deliberate: inlined, the switch grew about 1.2 KB of machine code per request type, and past nine types it stopped fitting the JIT's inlining budget — `Send(object)` went from 5.5 ns to 9.5 ns on a single added type, and kept climbing to 20 ns by eighty. Outlined, it stays flat.

## Overload Resolution

`Send(object)` is implemented as an **extension method** (not an interface method). This ensures the generated typed extension methods are always preferred when the compile-time type is known:

```csharp
// Typed extension wins — zero-overhead, no boxing
var result = await mediator.Send(new Ping()); // → ValueTask<int>

// Object extension — runtime dispatch, response boxed
object request = new Ping();
var result = await mediator.Send(request);   // → ValueTask<object?>
```

## Performance

Measured on .NET 11; .NET 10 is roughly twice as slow on both rows.

| Path | Cost | Boxing | Use case |
|---|---|---|---|
| `Send(new Ping())` | 2.7 ns | None | Normal application code |
| `Send((object)ping)` | 6.6 ns | `TResponse` → `object?` | Queue/bus consumers |

About 4 ns and one boxed response, against deserialization costs measured in microseconds on the paths that actually need this overload. Not a reason to avoid it where it fits.
