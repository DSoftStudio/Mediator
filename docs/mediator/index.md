---
layout: default
title: "DSoftStudio.Mediator Documentation"
description: "Ultra-low-latency mediator for .NET with compile-time dispatch, zero-allocation pipelines, and Native AOT support."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

# DSoftStudio.Mediator — Documentation

A source-generated mediator for .NET that compiles your pipeline ahead of time —
no reflection, no runtime composition, no hidden cost.

**No surprises. No hidden cost. No runtime magic.**

This documentation is organized to help you move from **quick onboarding**
to **deep architectural understanding**.

## Why DSoftStudio.Mediator?

Traditional mediator libraries discover handlers at runtime through reflection and
build their pipelines dynamically on every request. DSoftStudio.Mediator takes a
fundamentally different approach: a Roslyn source generator analyzes your code at
compile time, wires every handler, behavior, and processor into a strongly-typed
pipeline, and emits plain C# — no `Activator.CreateInstance`, no `Expression.Compile`,
no hidden allocations. The result is **near-zero dispatch overhead**, full
**Native AOT and IL-trimming compatibility**, and a pipeline you can step through
in the debugger just like any other method call.

---

## Getting Started

Start here if you're new to the library. Install the NuGet package, send your first request, and understand how handler registration order affects pipeline execution.

- [Installation](getting-started/installation.md) — Add the package and configure the source generator.
- [Quick Start](getting-started/quick-start.md) — Send a request and receive a response in under five minutes.
- [Registration Order](getting-started/registration-order.md) — Control the order in which behaviors and processors execute.
- [Migration from MediatR](getting-started/migration-from-mediatr.md) — Switch from MediatR with minimal code changes.

---

## Core Concepts

Learn the fundamental building blocks: how requests are dispatched to handlers, how notifications fan out, and how streams enable async enumeration.

- [Requests & Handlers](concepts/requests-and-handlers.md) — Define a request, implement a handler, and let the generated mediator wire them together.
- [Notifications](concepts/notifications.md) — Publish an event and have multiple handlers react independently.
- [Streams](concepts/streams.md) — Return results one at a time with `IAsyncEnumerable<T>` stream handlers.
- [CQRS (Commands & Queries)](concepts/cqrs.md) — Separate read and write paths using dedicated command and query types.

---

## Features

Advanced capabilities built on top of the core mediator. Add cross-cutting concerns, simplify handler authoring, and enable dynamic dispatch.

- [Pipeline Behaviors](features/pipeline-behaviors.md) — Wrap handler execution with logging, validation, transactions, or any custom logic.
- [Pre/Post Processors](features/pre-post-processors.md) — Run logic before or after a handler without writing a full behavior.
- [Self-Handling Requests](features/self-handling-requests.md) — Let the request itself act as the handler for simple use cases.
- [Runtime-Typed Dispatch (`Send(object)`)](features/runtime-dispatch.md) — Dispatch a request when its compile-time type is unknown.
- [Handler Validation](features/handler-validation.md) — Detect missing or duplicate handler registrations at startup.

---

## Integrations

Optional companion NuGet packages that plug into the pipeline with zero configuration overhead.

- [OpenTelemetry](integrations/opentelemetry.md) — Trace every request through the pipeline with automatic span creation.
- [FluentValidation](integrations/fluentvalidation.md) — Validate requests with FluentValidation rules before they reach the handler.
- [HybridCache](integrations/hybridcache.md) — Cache handler responses using .NET's `HybridCache` with attribute-driven invalidation.

---

## Architecture

Deep dive into the internal design. Understand how source generators build the dispatch pipeline at compile time and why every allocation is eliminated.

- [Dispatch Pipeline](architecture/dispatch-pipeline.md) — Step-by-step walkthrough of how a request travels through the generated pipeline.
- [Source Generators](architecture/source-generators.md) — How the Roslyn source generator discovers handlers and emits dispatch code.
- [Native AOT & Trimming](architecture/native-aot.md) — Full Native AOT and IL trimming support with zero runtime reflection.
- [Performance Design](architecture/performance.md) — Zero-allocation strategy, struct pipelines, and compile-time monomorphization.
- [Design Notes](architecture/design-notes.md) — Trade-offs, rejected alternatives, and rationale behind key decisions.
- [Production Validation](architecture/production-validation.md) — How the library is validated against real-world workloads.

---

## Advanced Usage

Patterns and advanced scenarios for production applications.

- [Caching Patterns](advanced/caching-patterns.md) — Implement read-through, write-through, and invalidation strategies with the mediator.
- [Pipeline Patterns](advanced/pipeline-patterns.md) — Compose behaviors and processors into reusable, testable pipeline configurations.

---

## Reference

- [Benchmarks](benchmarks.md) — BenchmarkDotNet results comparing DSoftStudio.Mediator against MediatR and Wolverine.
- [Changelog](changelog.md) — Release history with breaking changes, new features, and bug fixes.
- [GitHub Repository](https://github.com/DSoftStudio/Mediator) — Source code, issue tracker, and contribution guidelines.

---

## Architecture Decision Records (ADR)

Key design decisions behind the project, documented as lightweight ADRs for traceability.

- [ADR-0001: Architecture Overview](adr/0001-architecture-overview.md) — Why the library uses compile-time source generation instead of runtime reflection.
- [ADR-0002: Handler Discovery and Bug Avoidance](adr/0002-handler-discovery-and-bug-avoidance.md) — How exact-type dispatch eliminates the MediatR duplicate-handler bug.
- [ADR-0003: Fail-fast Handler Validation](adr/0003-fail-fast-handler-validation.md) — Startup validation that catches missing or misconfigured handlers early.
- [ADR-0004: Runtime-Typed Send(object)](adr/0004-runtime-typed-send.md) — Supporting dynamic dispatch without sacrificing compile-time safety.
- [ADR-0005: OpenTelemetry Instrumentation](adr/0005-opentelemetry-instrumentation.md) — Design of the tracing integration and span naming conventions.
