---
layout: default
title: "Empty pipeline tree - Pipeline Explorer"
description: "The Pipeline Explorer tree is empty after opening a solution. Causes and resolutions."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Troubleshooting](index.md)

# Empty pipeline tree

## Symptom

You opened a solution but the Pipeline Explorer view shows:

```
Request Pipelines (0)
Notifications (0)
Streams (0)
```

…even though your project contains mediator handlers.

This page walks through the five most common causes in order of likelihood.

---

## 1. No `.sln` file in the workspace

Pipeline Explorer discovers handlers per **solution**, not per folder. If you opened a bare folder or a single `.csproj`, the extension has nothing to scan.

**Check**

- VS Code: open the file explorer and confirm a `.sln` file is at the workspace root.
- Visual Studio: confirm a solution (not just a project) is open. The title bar shows the solution name.

**Fix**

Open the folder that contains the `.sln`, or open the `.sln` directly. Click **Refresh** in the toolbar after the workspace reloads.

---

## 2. No project references `DSoftStudio.Mediator`

The discovery step only scans projects that reference the `DSoftStudio.Mediator` NuGet package.

**Check**

Run from the solution root:

```shell
dotnet list package | findstr Mediator
```

(Use `grep Mediator` on macOS / Linux.) The output should include `DSoftStudio.Mediator`.

**Fix**

```shell
dotnet add package DSoftStudio.Mediator
```

After the package is added, build the solution once, then click **Refresh** in the Pipeline Explorer toolbar.

---

## 3. The solution has never been built

Pipeline Explorer relies on assembly metadata produced by the source generator. A solution that has never been compiled has no metadata to read.

**Check**

Look for `bin/` and `obj/` folders inside each project. If they are missing, the project has not been built.

**Fix**

```shell
dotnet build
```

Once the build succeeds, click **Refresh** in the toolbar.

---

## 4. Diagnostics are disabled in the project

If `DSoftMediatorDiagnosticsEnabled` is set to `false` in your `Directory.Build.props` or a project file, the analyzer skips emission and Pipeline Explorer sees no metadata.

**Check**

Search the solution for `DSoftMediatorDiagnosticsEnabled`. If it exists and is `false`, that is the cause.

**Fix**

Set the property to `true` (or remove it — the default is `true`):

```xml
<PropertyGroup>
  <DSoftMediatorDiagnosticsEnabled>true</DSoftMediatorDiagnosticsEnabled>
</PropertyGroup>
```

Rebuild and refresh.

> The VS Code extension exposes this toggle as the **`mediator.diagnosticsEnabled`** setting. Visual Studio exposes it under the gear icon in the tool window toolbar.

---

## 5. The server crashed or hasn't started

Pipeline Explorer talks to a bundled background server that performs the actual discovery. If the server failed to start, the tree stays empty.

**Check**

- VS Code: open the **Output** panel (`Ctrl+Shift+U`) and select **Mediator** from the dropdown. Look for `Server started` followed by a port number. If you see startup errors, see [server startup failed](server-startup-failed.md).
- Visual Studio: open **View → Output** and select **Mediator** in the **Show output from** dropdown.

**Fix**

Restart the server: run `Mediator: Stop Server` from the Command Palette (VS Code) or close and reopen the tool window (Visual Studio). The server respawns automatically on the next refresh.

If the restart does not help, see [server startup failed](server-startup-failed.md).

---

## Still stuck?

Open an issue at the [GitHub repository](https://github.com/DSoftStudio/Mediator.Enterprise/issues) with:

- Your IDE and version (VS Code 1.95, Visual Studio 17.13, etc.)
- The output of `dotnet list package | findstr Mediator`
- The Mediator output panel contents (last 20 lines)
- A minimal reproduction project, if possible

---

[← Back to Troubleshooting](index.md)
