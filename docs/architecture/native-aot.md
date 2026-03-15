<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Documentation](../index.md)

# Native AOT and Trimming

DSoftStudio.Mediator is fully compatible with .NET Native AOT publishing and IL trimming.

Both packages ship with `IsAotCompatible` and `IsTrimmable` enabled, and the trim analyzer is active at build time. The hot execution path uses no reflection, no `MakeGenericType`, no `Expression.Compile`, and no dynamic method generation — all handler discovery and dispatch wiring are performed at compile time by Roslyn source generators.

## Use Cases

This makes the mediator suitable for:

- **Native AOT ASP.NET applications** — publish self-contained, ahead-of-time compiled APIs
- **Serverless / cloud functions** — fast cold start with minimal memory footprint
- **Containerized microservices** — smaller images, no JIT warm-up
- **High-density cloud workloads** — reduced memory per instance

## AOT-Safe Runtime Dispatch

The `Publish(object)` and `Send(object)` overloads (runtime-typed dispatch) are also AOT-safe — they use compile-time generated `FrozenDictionary<Type, DispatchDelegate>` dispatch tables populated by the source generator, with no `MakeGenericType` at runtime.
