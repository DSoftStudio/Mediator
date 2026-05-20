---
layout: default
title: "Quick Start - Pipeline Explorer"
description: "Your first solution scan, profiling session, and source navigation in five minutes."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Pipeline Explorer](../index.md)

# Quick Start

By the end of this guide you will have scanned your first solution, navigated to a handler in one click, and watched live profiling data flow in.

This walkthrough assumes you have already [installed the extension](installation.md) and opened a solution that references `DSoftStudio.Mediator`. The steps look identical in Visual Studio Code and Visual Studio; screenshots are from VS Code unless noted.

---

## 1. Open your solution

Open a folder that contains a `.sln` file referencing `DSoftStudio.Mediator`. The Pipeline Explorer view auto-activates as soon as the `.sln` is detected:

- **VS Code** — click the **Mediator Pipelines** icon in the Activity Bar.
- **Visual Studio** — open **View → Other Windows → Mediator Pipeline Explorer**.

The tree populates with three sections:

```
Request Pipelines (n)
  CreateOrderCommand → OrderResult    Command · PassThrough
  GetOrderQuery → OrderDto            Query · Full
  …

Notifications (n)
  OrderPlacedNotification             3 handlers

Streams (n)
  GetOrderEventsStream → OrderEvent   PassThrough
```

The badge after each request pipeline tells you two things at a glance:

- **CQRS kind** — `Command`, `Query`, or `Request` (when neither marker interface is implemented).
- **Pipeline mode** — `PassThrough` (handler only), `BehaviorsOnly`, or `Full` (pre/post-processors + behaviors).

If the tree is empty, click **Refresh** in the toolbar. If it is still empty after refresh, see [Troubleshooting: empty tree](../troubleshooting/empty-tree.md).

---

## 2. Navigate to source

Click any node in the tree. The right-hand detail panel opens with:

- The request and response types
- The handler with its DI lifetime
- Every behavior, pre-processor, and post-processor in execution order
- The call sites that dispatch this pipeline (`controller.cs:42`, `worker.cs:118`)

Click any item — handler, behavior, or call site — and the IDE jumps to the exact line. No `Ctrl+T` hunt, no manual filename guessing.

<figure class="screenshot">
  <img src="../assets/screenshots/handler-detail.png" alt="Detail panel showing pipeline properties, execution order, and handler details with Open in Editor button">
  <figcaption>The detail panel for a selected pipeline — properties, execution order, and one-click navigation to source.</figcaption>
</figure>

> **Tip:** right-click any pipeline node for **Go to Handler**, **Copy Type Name**, **Find All References**, and **Pin to Top**.

---

## 3. Visualize the pipeline graph

The detail panel exposes a vertical "graph" toggle that opens the interactive graph below the detail.

- **Pan** — drag empty space.
- **Zoom** — mouse wheel.
- **Click a node** — navigates to source in the editor.
- **Hover an edge** — shows the dispatch context (behavior name, pre-processor order, notification fan-out).

The graph shows the full request flow: pre-processors → behaviors → handler → post-processors. Nested mediator calls inside the handler are drawn inline so you can see at a glance which other pipelines are triggered.

<figure class="screenshot">
  <img src="../assets/screenshots/graph-view.png" alt="Interactive pipeline graph showing Send to RegisterUserCommand to handler with notification fan-out to three handlers">
  <figcaption>The interactive graph for a request pipeline, with notification fan-out drawn inline so you see every effect a single dispatch triggers.</figcaption>
</figure>

---

## 4. Start runtime profiling

Profiling captures live timings as your code runs. The profiling hooks are wired into your application **automatically** by the analyzer — there is nothing to add to `Program.cs`. As long as the project that calls `services.AddMediator(...)` has the Pipeline Explorer analyzer loaded (which the extension auto-injects via `Directory.Build.props`), the hooks are emitted at compile time with zero allocation overhead until a profiling session is attached.

### Attach the profiler

- **VS Code** — Command Palette → `Mediator: Start Profiling`.
- **Visual Studio** — click **Start** (▶) in the tool window toolbar.

<figure class="screenshot">
  <img src="../assets/screenshots/runtime-profiler-empty.png" alt="Runtime Profiler panel in recording state with zero executions, waiting for traffic">
  <figcaption>The Runtime Profiler immediately after attaching — recording is on, the buffer is empty, and the panel is waiting for the first dispatch.</figcaption>
</figure>

Then issue requests against your application — run an integration test, exercise an endpoint, replay traffic. Within a second or two, the **Runtime Profiler** panel starts filling with rows like:

```
Handler                        Calls   Avg     p50    p95     p99    Errors
CreateOrderHandler                42    6.3 ms 5.1    14.8    38.2    0
ValidateOrderBehavior             42    1.2 ms 0.9     2.4     4.1    0
PersistOrderBehavior              42    3.4 ms 2.8     8.6    17.3    0
```

Each behavior is timed independently. Tail-heavy handlers — those with a p99 far above the median — are flagged so you can spot bottlenecks instantly. The default threshold (`tailHeavyMinP99Ms`) prevents false positives on sub-millisecond noise.

<figure class="screenshot">
  <img src="../assets/screenshots/runtime-profiler-active.png" alt="Runtime Profiler with 55 events captured, showing per-pipeline statistics, notification fanout, and request telemetry">
  <figcaption>Runtime Profiler populated with 55 events — per-pipeline statistics, notification fan-out timing, and live request telemetry side-by-side.</figcaption>
</figure>

### Find the slow one

Sort the table by **p99** or **Avg** to identify the worst offender. The **Hot Path · Flame** card below the invocations log shows where the time actually goes inside a slow request — handler self vs. nested handler latency vs. notification publish overhead — with a `bottleneck` tag on whichever step dominates.

<figure class="screenshot">
  <img src="../assets/screenshots/runtime-profiler-hot-path.png" alt="Hot Path flame card showing total 6.27 ms broken down by handler self, publish overhead, and three nested handlers with AuditLogHandler flagged as bottleneck">
  <figcaption>Hot Path · Flame — a per-request breakdown that surfaces the dominant cost (here, <code>AuditLogHandler</code> at 43% of total) with a single glance.</figcaption>
</figure>

Click the bottleneck row to jump to the source. Fix it, re-run, and watch the new numbers replace the old.

When you're done, click **Stop** to detach the profiler. Click **Clear** to reset the in-memory buffer, or **Snapshot** to capture the current state for later comparison.

---

## 5. Filter and search

As your codebase grows, the tree grows with it. Two tools keep it manageable:

- **Section filter** — the dropdown at the top of the toolbar narrows the tree to one of **All / Commands / Queries / Notifications / Streams**. Picking *Commands* leaves only `ICommand<>`-derived pipelines visible; *Queries* leaves only `IQuery<>`-derived ones.
- **Search box** — type any fragment of a type name, handler name, behavior name, or file path. Matches highlight and the tree collapses to the matching pipelines.

`Ctrl+F` (VS Code) or click the search box (VS) focuses the input. `Esc` clears it.

---

## What's next

You have everything you need to use Pipeline Explorer day-to-day. To go deeper, see:

- The runtime profiler reference (coming soon) — full p50 / p95 / p99 / max / error rate semantics.
- The settings reference (coming soon) — every toggle that controls the tree, graph, and profiler.
- The troubleshooting guide (coming soon) — top issues and resolutions reported by early users.

If something behaves unexpectedly, please file a report at the [GitHub repository](https://github.com/DSoftStudio/Mediator.Enterprise/issues) — every bug filed in Early Access becomes a fixed regression in the next release.

---

[← Back to Pipeline Explorer](../index.md)
