---
layout: default
title: "Build errors after installing - Pipeline Explorer"
description: "Your build broke after installing Pipeline Explorer. Causes and resolutions."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Troubleshooting](index.md)

# Build errors after installing

## Symptom

Your solution built cleanly before installing Pipeline Explorer. After installation, `dotnet build` produces compiler errors in generated files under `obj/Debug/.../DSoftStudio.Mediator.Profiling/` or similar, with messages such as:

```
error CS0234: The type or namespace name 'Abstractions' does not exist in the namespace
'DSoftStudio.Mediator' (are you missing an assembly reference?)
```

or

```
error CS0234: The type or namespace name 'IServiceCollection' does not exist in the namespace
'Microsoft.Extensions.DependencyInjection' (are you missing an assembly reference?)
```

Pipeline Explorer installs a Roslyn analyzer **machine-locally**. The MSBuild block that enables it is injected under `$(MSBuildUserExtensionsPath)\DSoftStudio.Mediator\enabled.props` (on Windows, `%LOCALAPPDATA%\Microsoft\MSBuild\DSoftStudio.Mediator\enabled.props`), which MSBuild auto-imports into every project you build on this machine — **nothing is written to your repository**. (Older versions injected this block into the solution's `Directory.Build.props`; that repo-level injection is no longer used, and any leftover markers are stripped on solution open.) The analyzer ships a source generator that emits profiling and visualization code into every project that picks it up — which can collide with projects that don't reference the types the generated code needs.

---

## 1. Pure projects without `DSoftStudio.Mediator.Abstractions`

The classic case: a Clean Architecture solution where the `Domain` project intentionally has zero dependencies. The machine-local analyzer import applies to the Domain project too, and the generator emits code referencing `DSoftStudio.Mediator.Abstractions` and `Microsoft.Extensions.DependencyInjection` — neither of which the Domain project references.

This produces the `CS0234 Abstractions does not exist` errors listed above.

**Check**

Look at the project that has the errors. If it does **not** reference `DSoftStudio.Mediator` (directly or transitively), this is your cause.

**Fix**

Current versions of Pipeline Explorer gate emission on reference presence — the generator detects that the consuming project doesn't reference the required types and emits nothing. Make sure you are on the latest extension:

- **VS Code** — Extensions view → search `DSoftStudio Mediator` → click **Update** if available.
- **Visual Studio** — `Extensions → Manage Extensions → Updates`.

If you are already on the latest and still see the errors, [open an issue](https://github.com/DSoftStudio/Mediator.Enterprise/issues) with the exact error text and the name of the project that has no `DSoftStudio.Mediator` reference — no source needed.

> **Workaround for older versions**: scope the analyzer to specific projects by deleting the `<ItemGroup>` block at the solution root's `Directory.Build.props` and pasting it only into the projects that reference `DSoftStudio.Mediator`.

---

## 2. Stale generator cache

Source generators are cached aggressively by MSBuild. After an extension update, the old generator DLL can still be referenced from `obj/` until a clean rebuild.

**Check**

Run `dotnet build` twice. If the errors are different on the second run, or disappear entirely, you were hitting a cache issue.

**Fix**

```shell
dotnet clean
git clean -xfd        # caution: removes all untracked + ignored files
dotnet restore
dotnet build
```

If you can't `git clean -xfd` (untracked files you need to keep), at minimum:

```shell
dotnet clean
rm -rf */obj */bin    # PowerShell: Remove-Item -Recurse -Force */obj,*/bin
dotnet build
```

---

## 3. Machine-local injection and leftover repo blocks

Pipeline Explorer no longer writes anything to your repository. When you enable it, the extension writes the analyzer import **machine-locally**, under MSBuild's per-user extension point:

- the enablement registry → `$(MSBuildUserExtensionsPath)\DSoftStudio.Mediator\enabled.props`
- an `ImportBefore` targets file that MSBuild auto-imports into every SDK build on the machine

On Windows both resolve under `%LOCALAPPDATA%\Microsoft\MSBuild\`. Because nothing lands in the repo, **your solution's `Directory.Build.props` is not touched and will not contain any Pipeline Explorer markers** — that is the normal, expected state, not a sign that injection failed. Toggling **Diagnostics enabled** off/on rewrites the machine-local files; it will **not** make marker comments appear in your repo.

> **Older versions** injected the block directly into the solution's `Directory.Build.props`, wrapped in marker comments — `<!-- BEGIN DSoftStudio.Mediator (auto-injected by VS Code extension) -->` (VS Code) or `<!-- BEGIN DSoftStudio.Mediator (auto-injected by VSIX) -->` (Visual Studio). Current versions strip any such block on solution open. If you find one, an outdated tool wrote it.

**Check**

Confirm the machine-local registry exists (Windows PowerShell):

```powershell
Get-Content "$env:LOCALAPPDATA\Microsoft\MSBuild\DSoftStudio.Mediator\enabled.props"
```

If the file is missing, the extension has not enabled diagnostics on this machine yet.
If it exists **and** you also have a leftover marker block in your repo's `Directory.Build.props`, the stale repo copy can reference an old analyzer path and conflict with the machine-local one.

**Fix**

- **Registry missing**: open the Pipeline Explorer settings panel and toggle **Diagnostics enabled** off, then on. The machine-local files are rewritten.
- **Leftover repo block**: delete the marker-wrapped block from your solution's `Directory.Build.props` (current versions do this automatically on solution open). The machine-local import supersedes it.

---

## 4. Diagnostics conflicting with another analyzer

If your solution also references `MediatR.Extensions.Microsoft.DependencyInjection`, `Wolverine`, or any other mediator-style library with its own analyzers, the analyzers can produce duplicate or conflicting diagnostics on the same code.

**Check**

Look at the diagnostic ID. Pipeline Explorer's diagnostics are prefixed `DSOFT***` (e.g. `DSOFT103`). If your build errors / warnings come from a different prefix, the issue is in another analyzer, not Pipeline Explorer.

**Fix**

If you intentionally have both libraries in the same solution, scope each analyzer to its own projects. The Pipeline Explorer analyzer can be disabled solution-wide by setting:

```xml
<PropertyGroup>
  <DSoftMediatorDiagnosticsEnabled>false</DSoftMediatorDiagnosticsEnabled>
</PropertyGroup>
```

…in your `Directory.Build.props`. This disables only Pipeline Explorer's analyzer; it does not affect the runtime mediator library.

> **Note**: `DSoftMediatorDiagnosticsEnabled` only takes effect when your project references the `DSoftStudio.Mediator.Diagnostics` NuGet package. If Pipeline Explorer was installed via the IDE extension (the usual case), this property is **not** `CompilerVisible` on the machine-local injected path, so setting it silently does nothing. In that case, disable the analyzer from the Pipeline Explorer **Settings panel → Diagnostics** toggle instead.

> Disabling the analyzer hides Pipeline Explorer from the IDE. To get the tree back, re-enable diagnostics and reload the IDE.

---

## 5. Strong-name signing conflicts

If your solution uses strong-name signing and pins to a specific public key, the Pipeline Explorer analyzer (which is itself strong-named) can collide with a project-level `<SignAssembly>` policy.

**Check**

Build errors mention `CS8002: Referenced assembly does not have a strong name` or `MSB3277: Found conflicts between different versions`.

**Fix**

Pipeline Explorer's assemblies are strong-named. If your project enforces a specific public key, exclude the Pipeline Explorer assemblies from strong-name validation, or use a binding redirect.

For most users this is not a real issue — strong-name conflicts only matter in tightly-controlled enterprise environments.

---

## Quick disable as a workaround

If you need to ship a build immediately and the errors are blocking you, disable the Pipeline Explorer profiling generator for the broken build. The `CS0234` errors come from the profiling source generator, which is gated on `DSoftMediatorProfilingEnabled` — pass it as `false`:

```shell
dotnet build -p:DSoftMediatorProfilingEnabled=false
```

This produces a clean build but disables the profiling code generation for that invocation. The IDE tree will not populate profiling data until you build again without the override.

---

[← Back to Troubleshooting](index.md)
