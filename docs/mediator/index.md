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

DSoftStudio.Mediator is an ultra-low-latency mediator for .NET with compile-time dispatch,
zero-allocation pipelines, and Native AOT compatibility.

This documentation is organized to help you move from **quick onboarding**
to **deep architectural understanding**.

---

## Getting Started

Start here if you're new to the library.

- [Installation](getting-started/installation.md)
- [Quick Start](getting-started/quick-start.md)
- [Registration Order](getting-started/registration-order.md)
- [Migration from MediatR](getting-started/migration-from-mediatr.md)

---

## Core Concepts

Learn the fundamental building blocks of the mediator.

- [Requests & Handlers](concepts/requests-and-handlers.md)
- [Notifications](concepts/notifications.md)
- [Streams](concepts/streams.md)
- [CQRS (Commands & Queries)](concepts/cqrs.md)

---

## Features

Advanced capabilities built on top of the core mediator.

- [Pipeline Behaviors](features/pipeline-behaviors.md)
- [Pre/Post Processors](features/pre-post-processors.md)
- [Self-Handling Requests](features/self-handling-requests.md)
- [Runtime-Typed Dispatch (`Send(object)`)](features/runtime-dispatch.md)
- [Handler Validation](features/handler-validation.md)

---

## Integrations

Optional companion packages that extend the mediator.

- [OpenTelemetry](integrations/opentelemetry.md)
- [FluentValidation](integrations/fluentvalidation.md)
- [HybridCache](integrations/hybridcache.md)

---

## Architecture

Deep dive into the internal design.

- [Dispatch Pipeline](architecture/dispatch-pipeline.md)
- [Source Generators](architecture/source-generators.md)
- [Native AOT & Trimming](architecture/native-aot.md)
- [Performance Design](architecture/performance.md)

---

## Advanced Usage

Patterns and advanced scenarios.

- [Caching Patterns](advanced/caching-patterns.md)
- [Pipeline Patterns](advanced/pipeline-patterns.md)

---

## Reference

- [Benchmarks](benchmarks.md)
- [Changelog](https://github.com/DSoftStudio/Mediator/blob/main/CHANGELOG.md)

---

## Architecture Decision Records (ADR)

Key design decisions behind the project.

- [ADR-0001: Architecture Overview](adr/0001-architecture-overview.md)
- [ADR-0002: Handler Discovery and Bug Avoidance](adr/0002-handler-discovery-and-bug-avoidance.md)
- [ADR-0003: Fail-fast Handler Validation](adr/0003-fail-fast-handler-validation.md)
- [ADR-0004: Runtime-Typed Send(object)](adr/0004-runtime-typed-send.md)
- [ADR-0005: OpenTelemetry Instrumentation](adr/0005-opentelemetry-instrumentation.md)
