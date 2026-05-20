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

Pipeline Explorer installs a Roslyn analyzer at the solution level via `Directory.Build.props`. The analyzer ships a source generator that emits profiling and visualization code into every project that picks the analyzer up — which can collide with projects that don't reference the types the generated code needs.

---

## 1. Pure projects without `DSoftStudio.Mediator.Abstractions`

The classic case: a Clean Architecture solution where the `Domain` project intentionally has zero dependencies. The solution-level `Directory.Build.props` injects the Pipeline Explorer analyzer into the Domain project too, and the generator emits code referencing `DSoftStudio.Mediator.Abstractions` and `Microsoft.Extensions.DependencyInjection` — neither of which the Domain project references.

This produces the `CS0234 Abstractions does not exist` errors listed above.

**Check**

Look at the project that has the errors. If it does **not** reference `DSoftStudio.Mediator` (directly or transitively), this is your cause.

**Fix**

Pipeline Explorer **0.1.0 and later** gates emission on reference presence. The generator detects that the consuming project doesn't reference the required types and emits nothing. Make sure you are on the latest extension:

- **VS Code** — Extensions view → search `DSoftStudio Mediator` → click **Update** if available.
- **Visual Studio** — `Extensions → Manage Extensions → Updates`.

If you are already on the latest and still see the errors, [open an issue](https://github.com/DSoftStudio/Mediator.Enterprise/issues) with the project file (`*.csproj`) attached.

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

## 3. `Directory.Build.props` collision

If your solution already has a custom `Directory.Build.props` at the root, Pipeline Explorer's install routine appends to it. A pre-existing `<PropertyGroup>` or `<ItemGroup>` with conflicting settings can shadow the extension's additions.

**Check**

Open `Directory.Build.props` at the solution root. Look for the marker comments:

```xml
<!-- BEGIN DSoftStudio.Mediator (auto-injected by VS Code extension) -->
...
<!-- END DSoftStudio.Mediator -->
```

If those markers are missing, the extension never injected its block.
If those markers are present but a duplicate analyzer reference exists elsewhere, you have a conflict.

**Fix**

- **Missing block**: open the Pipeline Explorer settings panel and toggle **Diagnostics enabled** off, then on. The injection runs again.
- **Duplicate**: remove the non-extension copy and keep only the block between the marker comments.

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

> Disabling the analyzer hides Pipeline Explorer from the IDE. To get the tree back, re-enable diagnostics and reload the IDE.

---

## 5. Strong-name signing conflicts

If your solution uses strong-name signing and pins to a specific public key, the Pipeline Explorer analyzer (which is itself strong-named) can collide with a project-level `<SignAssembly>` policy.

**Check**

Build errors mention `CS8002: Referenced assembly does not have a strong name` or `MSB3277: Found conflicts between different versions`.

**Fix**

Pipeline Explorer assemblies are signed with `PublicKeyToken=6c7e753832e8eb05`. If your project pins a different token, exclude the Pipeline Explorer assemblies from the strong-name validation list, or use a binding redirect.

For most users this is not a real issue — strong-name conflicts only matter in tightly-controlled enterprise environments.

---

## Quick disable as a workaround

If you need to ship a build immediately and the errors are blocking you, disable the Pipeline Explorer analyzer for the broken build:

```shell
dotnet build -p:DSoftMediatorDiagnosticsEnabled=false
```

This produces a clean build but disables the analyzer and the source generator for that invocation. The IDE tree will not populate until you re-enable diagnostics.

---

## Still stuck?

Open an issue at the [GitHub repository](https://github.com/DSoftStudio/Mediator.Enterprise/issues) with:

- The full error output from `dotnet build`
- Your `Directory.Build.props` (root and any nested copies)
- The `.csproj` of the affected project
- Your IDE and extension version

Include `obj/Debug/.../DSoftStudio.Mediator.Profiling/*.g.cs` for the affected project if the errors point at a generated file — it helps us reproduce.

---

[← Back to Troubleshooting](index.md)
