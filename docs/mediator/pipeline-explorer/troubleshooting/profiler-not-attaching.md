---
layout: default
title: "Profiler not attaching - Pipeline Explorer"
description: "You started runtime profiling but no events appear. Causes and resolutions."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Troubleshooting](index.md)

# Profiler not attaching

## Symptom

You clicked **Start Profiling** and exercised your application, but the Runtime Profiler panel stays empty:

```
Handler            Calls   Avg   p50   p95   p99   Errors
(no data)
```

…or only some handlers appear and others don't.

When everything is wired correctly the detail panel of a selected handler shows live runtime statistics — calls, p50 / p95 / p99, max, and total — that update as new events arrive:

<figure class="screenshot">
  <img src="../assets/screenshots/handler-runtime-stats.png" alt="Handler detail panel with Runtime Statistics table showing 55 calls, 0 errors, percentiles, and last run timestamp">
  <figcaption>What a working profiling session looks like — the Runtime Statistics table fills in for every handler that executed since the profiler attached.</figcaption>
</figure>

The pipeline graph below the detail also overlays per-component timing once data flows:

<figure class="screenshot">
  <img src="../assets/screenshots/graph-view-timing.png" alt="Pipeline graph with timing overlays showing handler 6.27 ms avg, publish 6.02 ms, and three nested handlers with their durations">
  <figcaption>Graph view with timing overlays — each node carries its average duration and the dispatch offsets relative to the request start.</figcaption>
</figure>

> **Important**: Pipeline Explorer wires `AddMediatorProfiling()` into your application **automatically** via C# 12 interceptors. You do NOT need to add any line to `Program.cs` — referencing the analyzer (which the extension auto-injects into your solution) is enough. If profiling isn't working, the wiring failed for one of the reasons below.

---

## 1. The application isn't actually running

The IDE button only **enables** profiling on the IDE side. Events flow only when your application is running and dispatching requests through the mediator. Clicking **Start** without running the application produces a `Profiling started — waiting for events…` state that never updates.

**Check**

- Is your API / worker / test runner actually started?
- Are requests being dispatched? (Curl an endpoint, fire a test, replay a message.)

**Fix**

Run the application and issue at least one request that goes through the mediator pipeline. Within a second or two, events should appear in the Profiler panel.

If you are running the application from inside the same IDE (F5 in Visual Studio, "Run and Debug" in VS Code), confirm the run target is the project that wires `AddMediator(...)`. A common mistake is to run a different startup project that doesn't call into the mediator.

---

## 2. Profiling was disabled at build time

The auto-wiring is gated on the MSBuild property `DSoftMediatorProfilingEnabled`. When it is `false`, the source generator emits nothing and the auto-registration disappears entirely — no profiling code exists in the produced IL.

The default value is `true`, but the extension's Settings panel exposes a toggle that flips this property in your solution's `Directory.Build.props`. If you (or someone on your team) ever turned profiling off, the toggle persists.

**Check**

Search your solution for `DSoftMediatorProfilingEnabled`. If you find it set to `false`, that is the cause:

```xml
<DSoftMediatorProfilingEnabled>false</DSoftMediatorProfilingEnabled>
```

**Fix**

Set it to `true` (or remove the property — the default is `true`):

```xml
<PropertyGroup>
  <DSoftMediatorProfilingEnabled>true</DSoftMediatorProfilingEnabled>
</PropertyGroup>
```

Then rebuild the affected project so the source generator runs again. The extension also exposes this toggle in the Settings panel (gear icon in the toolbar).

---

## 3. The analyzer isn't loaded in the project that calls `AddMediator`

The auto-registration only fires in projects that pick up the Pipeline Explorer analyzer **and** reference `DSoftStudio.Mediator.Abstractions`. If your composition root lives in a project that doesn't satisfy both conditions, the interceptors are never generated and profiling never wires itself in.

**Check**

In the project that contains your `services.AddMediator(...)` call:

- Confirm `DSoftStudio.Mediator` is referenced (directly or transitively):

  ```shell
  dotnet list <project>.csproj package | findstr Mediator
  ```

- Confirm the analyzer is loaded. The simplest way is to look for a `Directory.Build.props` at the solution root with this block:

  ```xml
  <!-- BEGIN DSoftStudio.Mediator (auto-injected by VS Code extension) -->
  <ItemGroup ...>
    <Analyzer Include="...DSoftStudio.Mediator.Profiling.dll" />
    ...
  </ItemGroup>
  <!-- END DSoftStudio.Mediator -->
  ```

  If that block is missing, the analyzer never got injected (or was removed).

**Fix**

- If the package reference is missing: `dotnet add package DSoftStudio.Mediator`.
- If the `Directory.Build.props` block is missing: open the extension's Settings panel, toggle **Diagnostics enabled** off, then on. The auto-injection runs again.
- Rebuild.

---

## 4. The application uses a different mediator instance

Profiling auto-injects at the `services.AddMediator(...)` call site. If your application creates a separate mediator instance via `new Mediator(...)` or a side `IServiceCollection`, that instance is invisible to the profiler.

**Check**

Look for any code that constructs a mediator directly or builds a second container:

```csharp
var mediator = new Mediator(...);          // ← profiler can't see this
var services = new ServiceCollection();    // ← second container, also invisible
services.AddMediator(...);
```

**Fix**

Resolve `IMediator` from the same DI container that received the `AddMediator(...)` call your application boots with. If you have a legitimate reason to use a second container (testing, isolated subsystems), the same auto-registration fires there too — it just needs to call through `AddMediator(...)`.

---

## 5. The buffer is full and dropping events

Pipeline Explorer caps profiling events at `mediator.maxBufferedEvents` (default `2000`). When the buffer is full, oldest events are dropped FIFO. If you fire 10,000 requests in a few seconds with the default cap, you may see only the most recent ~2,000.

**Check**

The Profiler panel shows a buffer indicator (`1,847 / 2,000 events`). If it pegs at the max and earlier events are missing, the buffer is your bottleneck.

**Fix**

Raise the cap:

```jsonc
// settings.json (VS Code)
{
  "mediator.maxBufferedEvents": 20000
}
```

…or open the gear panel in Visual Studio and bump the value there. The cap is per-session and resets to default on the next IDE restart. Buffer memory is proportional to the cap — `20000` events is on the order of 2 MB.

For sustained high-volume traffic, use **Snapshot** to capture batches of data into named exports instead of relying on the live buffer.

---

## 6. Direct handler resolution bypasses the mediator

If specific handlers are missing but the panel is otherwise populating, the most likely cause is **handler resolution outside the mediator dispatch path**. The profiler instruments `IMediator.Send()` / `IMediator.Publish()`; direct handler invocation bypasses the hooks.

**Check**

Search for direct handler resolution:

```csharp
var handler = serviceProvider.GetRequiredService<IRequestHandler<MyCmd, Result>>();
await handler.Handle(cmd, ct);     // ← bypasses mediator, no event emitted
```

**Fix**

Dispatch through the mediator:

```csharp
var mediator = serviceProvider.GetRequiredService<IMediator>();
await mediator.Send(cmd, ct);      // ← profiled
```

This is the same pattern that gives you pipeline behaviors — direct invocation also bypasses every behavior, not just the profiler.

---

## 7. The host is short-lived

Console apps and integration tests that exit immediately after a single dispatch may finish before the profiling event reaches the IDE. The transport is asynchronous; the host has to live at least a few hundred milliseconds after the last dispatch.

**Check**

Is your application a console app that exits right after `mediator.Send(...)`?

**Fix**

Either:

- Use **Snapshot** instead of live streaming for short-lived processes — it forces a synchronous flush before the snapshot completes.
- Add a small delay at the end of your test / console run:

  ```csharp
  await Task.Delay(500);   // give the profiler time to flush
  ```

For long-running services (APIs, workers, daemons), this is not an issue.

---

## Still stuck?

Open an issue at the [GitHub repository](https://github.com/DSoftStudio/Mediator.Enterprise/issues) with:

- The output of `dotnet list package | findstr Mediator`
- Your composition-root code (the part that calls `AddMediator`)
- Whether `DSoftMediatorProfilingEnabled` appears anywhere in your `Directory.Build.props` or `.csproj` files
- The Mediator output panel contents (last 50 lines)
- The IDE version + extension version

---

[← Back to Troubleshooting](index.md)
