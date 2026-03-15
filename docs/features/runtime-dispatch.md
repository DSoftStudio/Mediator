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

The `Send(object)` overload uses a compile-time generated `FrozenDictionary<Type, DispatchDelegate>` dispatch table — same architecture as `Publish(object)`. No reflection, no `MakeGenericType`, fully AOT-safe.

## How It Works

- The source generator registers a dispatch delegate for every request type discovered at compile time
- At runtime, `Send(object)` looks up the delegate by `request.GetType()` (O(1) frozen dictionary lookup)
- The delegate casts the object to the concrete request type, resolves the handler/pipeline, and returns the response boxed as `object?`
- `TResponse` boxing only occurs on this path — the standard `Send<TRequest, TResponse>()` path remains zero-allocation

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

| Path | Lookup | Boxing | Use case |
|---|---|---|---|
| `Send(new Ping())` | Static generic (~7 ns) | None | Normal application code |
| `Send((object)ping)` | FrozenDictionary (~2-5 ns) | `TResponse` → `object?` | Queue/bus consumers |

The FrozenDictionary lookup + boxing cost is negligible compared to the deserialization cost (~μs) in queue/bus scenarios.
