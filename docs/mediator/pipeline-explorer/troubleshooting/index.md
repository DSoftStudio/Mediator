---
layout: default
title: "Troubleshooting - Pipeline Explorer"
description: "Top issues reported by Pipeline Explorer users, with step-by-step resolutions."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Pipeline Explorer](../index.md)

# Troubleshooting

Quick links to the most common issues. Each page walks through likely causes in order of frequency and gives concrete resolution steps.

## Tree and discovery

- [Empty pipeline tree](empty-tree.md) — The view shows `(0)` counts even though your project has handlers.
- [Build errors after installing](analyzer-errors.md) — The solution built cleanly before installation; now it doesn't.

## Server and runtime

- [Server startup failed](server-startup-failed.md) — The bundled background server can't start or crashes immediately.
- [Profiler not attaching](profiler-not-attaching.md) — Runtime profiling shows no events even though your application is running.

## Licensing

- [License activation failed](activation-failed.md) — Your license file was rejected, the seat limit was reached, or activation couldn't complete.

---

## Known issues

### Handler / component "Open in Editor" is disabled until a full rebuild (layered solutions)

**Symptom** — In a multi-project solution where a handler or a pipeline component (a pre/post-processor or behavior) is defined in a **referenced** project — while the `Send` / `Publish` call site lives in the host project — the detail panel's **FILE** field is blank and **Open in Editor** / **Copy File Path** are disabled for that handler or component. Call sites still resolve normally, and the pipeline, counts, and profiling are all correct.

**Why** — To open a solution quickly, the first discovery reads the pipeline maps produced at build time (one per project). In the *host* project's map, a handler/component that lives in a referenced project is recorded as an external symbol with **no source path** — the source path is present only in the *owner* project's own map. An incremental build may not have regenerated the owner project's map, so the source path isn't available yet.

**Workaround** — **Rebuild the solution** (Visual Studio: `Build → Rebuild Solution`; VS Code / CLI: `dotnet build --no-incremental`, or clean the `obj` folders and build). A full rebuild regenerates every project's map and re-runs the complete analysis, so the handler/component source paths resolve — **FILE**, **Open in Editor**, and **Copy File Path** then work.

---

## Diagnostic checklist before reporting

If you plan to open an issue at the [GitHub repository](https://github.com/DSoftStudio/Mediator.Enterprise/issues), collecting these four items up front cuts the round-trip in half:

1. **IDE and extension version**
   - VS Code: `Help → About` and Extensions view → click the extension → version on the right.
   - Visual Studio: `Help → About Microsoft Visual Studio`, then `Extensions → Manage Extensions → Installed → DSoftStudio Mediator Pipeline Explorer` for the extension version.
2. **Mediator output panel** (last 50 lines)
   - VS Code: `Ctrl+Shift+U` → select **Mediator Server** in the dropdown.
   - Visual Studio: `View → Output` → select **DSoftStudio Mediator** in the dropdown.
3. **Package list**

   ```shell
   dotnet list package | findstr Mediator
   ```

   (Use `grep Mediator` on macOS / Linux.)

4. **OS and architecture**

   ```shell
   uname -a              # macOS / Linux
   systeminfo | findstr "OS"   # Windows
   ```

---

[← Back to Pipeline Explorer](../index.md)
