# ADR-0003: Strong Naming

**Status:** Approved — Scheduled for v1.2.0  
**Date:** 2026-03-25

---

## Context

Users with strong-named (signed) projects cannot reference DSoftStudio.Mediator assemblies because they ship with `PublicKeyToken=null`. The CLR enforces that a signed assembly cannot reference an unsigned assembly.

---

## Decision

Add strong naming to all published assemblies using a self-generated `.snk` key committed to the repository.

**Strong naming ≠ code signing.** The `.snk` provides assembly identity only — not publisher verification. This is standard practice for OSS libraries (Newtonsoft.Json, AutoMapper, MassTransit, Serilog).

| Concern | Mechanism | Status |
|---|---|---|
| `PublicKeyToken=null` blocks signed consumers | **Strong naming (.snk)** | ← This ADR |
| Verify publisher identity on NuGet | **Code signing (SignPath/DigiCert)** | Future |
| Prevent fake packages on nuget.org | **NuGet prefix reservation** | Verify |

---

## Scope

### Assemblies to sign

| Assembly | Ships as | Must sign |
|---|---|---|
| `DSoftStudio.Mediator.Abstractions` | NuGet package | ✅ |
| `DSoftStudio.Mediator` | NuGet package | ✅ |
| `DSoftStudio.Mediator.Generators` | Embedded analyzer in Mediator package | ✅ |
| `DSoftStudio.Mediator.FluentValidation` | NuGet package | ✅ |
| `DSoftStudio.Mediator.OpenTelemetry` | NuGet package | ✅ |
| `DSoftStudio.Mediator.HybridCache` | NuGet package | ✅ |
| Test projects | Not published | ✅ (required for `InternalsVisibleTo`) |
| Sample projects | Not published | ✅ (via `Directory.Build.props`) |

### InternalsVisibleTo declarations to update

| Source project | Friend assembly |
|---|---|
| `DSoftStudio.Mediator` | `DSoftStudio.Mediator.Tests` |
| `DSoftStudio.Mediator.FluentValidation` | `DSoftStudio.Mediator.FluentValidation.Tests` |
| `DSoftStudio.Mediator.HybridCache` | `DSoftStudio.Mediator.HybridCache.Tests` |
| `DSoftStudio.Mediator.OpenTelemetry` | `DSoftStudio.Mediator.OpenTelemetry.Tests` |
| `DSoftStudio.Mediator.InternalsVisibleTo.Host` | `DSoftStudio.Mediator.InternalsVisibleTo.Tests` |

⚠️ **With strong naming, `InternalsVisibleTo` requires the public key.** If a signed assembly declares `InternalsVisibleTo` without a public key, the compiler emits **CS1726**. The .NET SDK resolves this automatically **only if** the `$(PublicKey)` property is defined in MSBuild (see step 2). Without that property → compile error in CI/Release.

> **Ref:** [CS1726](https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/cs1726) — *"Strong-name signed assemblies must specify a public key in their InternalsVisibleTo declarations."*
> **Ref:** [MSBuild `InternalsVisibleTo` item](https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#internalsvisibleto) — *"If you don't specify `Key` metadata and a `$(PublicKey)` is available, that key is used. Otherwise, no public key is added to the attribute."*

---

## Implementation Checklist

### 1. Generate `.snk`

```shell
sn -k DSoftStudio.Mediator.snk
```

Place at repository root. Commit to source control.

### 2. Configure `Directory.Build.props`

Create at repository root — applies to all projects:

```xml
<Project>
  <PropertyGroup>
    <SignAssembly>true</SignAssembly>
    <AssemblyOriginatorKeyFile>$(MSBuildThisFileDirectory)DSoftStudio.Mediator.snk</AssemblyOriginatorKeyFile>
    <!-- PublicKey is required for InternalsVisibleTo to work with strong-named assemblies.
         Without this, MSBuild generates [InternalsVisibleTo("X")] without the public key,
         causing CS1726 at compile time.
         Extract with: sn -p DSoftStudio.Mediator.snk public.key && sn -tp public.key -->
    <PublicKey><!-- PASTE FULL PUBLIC KEY HERE AFTER GENERATING .snk --></PublicKey>
  </PropertyGroup>
</Project>
```

> **Important:** `$(PublicKey)` is what allows the existing `<InternalsVisibleTo Include="X" />` items in each csproj to work without manually adding `Key="..."` to each one. The .NET SDK automatically injects `$(PublicKey)` when generating the `[InternalsVisibleTo]` attribute.
>
> To extract the public key from the `.snk`:
> ```shell
> sn -p DSoftStudio.Mediator.snk DSoftStudio.Mediator.pub
> sn -tp DSoftStudio.Mediator.pub
> ```
> Copy the full hexadecimal value (no spaces) into `<PublicKey>`.

### 3. Update `InternalsVisibleTo`

**No changes required in the `.csproj` files** — the existing items (`<InternalsVisibleTo Include="X" />`) continue to work **because `$(PublicKey)` is defined in `Directory.Build.props`** (step 2).

The .NET SDK automatically generates:
```csharp
[assembly: InternalsVisibleTo("DSoftStudio.Mediator.Tests, PublicKey=00240000048...")]
```

⚠️ **Without `$(PublicKey)` in MSBuild**, the SDK generates the attribute WITHOUT the public key → **CS1726** at compile time.

**Verification:**
```shell
# After build, confirm the attribute includes PublicKey:
ildasm /metadata /text src/DSoftStudio.Mediator/bin/Release/net8.0/DSoftStudio.Mediator.dll | findstr InternalsVisibleTo
```

Expected output: `InternalsVisibleTo("DSoftStudio.Mediator.Tests, PublicKey=...")`.

### 4. Validate tests

```shell
dotnet test --configuration Release
```

All 312 tests must pass. Strong naming must not break:
- Source generator output
- Interceptor code generation
- `InternalsVisibleTo` access in test projects
- Cross-project handler discovery (`ModularMonolith` tests)

### 5. Verify `PublicKeyToken`

```shell
sn -Tp src/DSoftStudio.Mediator.Abstractions/bin/Release/netstandard2.0/DSoftStudio.Mediator.Abstractions.dll
sn -Tp src/DSoftStudio.Mediator/bin/Release/net8.0/DSoftStudio.Mediator.dll
```

Confirm `PublicKeyToken ≠ null` for all published assemblies.

### 6. Verify CI

- GitHub Actions workflow must build and test without modifications
- `dotnet build` and `dotnet pack` must produce signed assemblies
- No additional CI configuration required (`.snk` is in the repo)

### 7. Publish new version

- Bump version to **1.2.0** (minor version — new capability, no breaking changes)
- Update all companion packages (FluentValidation, OpenTelemetry, HybridCache) to **1.1.0**
- Release notes: "All assemblies are now strong-named. Projects with signed assemblies can reference DSoftStudio.Mediator without compilation errors."

---

## Security Considerations

| Question | Answer |
|---|---|
| Does the `.snk` in the repo compromise security? | No. Strong naming provides identity, not security. Microsoft docs: *"Do not rely on strong names for security. They provide a unique identity only."* |
| Can someone build a fake package with our `.snk`? | Yes — same as today without `.snk`. Protection against fake packages comes from NuGet prefix reservation and code signing, not strong naming. |
| Should we use `PublicSign` instead of full signing? | No. Full signing is more broadly compatible. `PublicSign` can fail in some legacy environments. |
| Is the `.snk` in `.gitignore`? | No — it must be committed. All major OSS libraries commit their `.snk`. |

---

## Breaking Change Assessment

**This is NOT a breaking change** for existing consumers:

- Unsigned assemblies can reference signed assemblies (always works)
- Signed assemblies can now reference our assemblies (previously blocked — this is the fix)
- API surface: zero changes
- Behavior: zero changes
- Binary compatibility: existing unsigned consumers continue to work

**Minor version bump** (1.1.8 → 1.2.0) is appropriate because strong naming adds a new capability without breaking existing consumers.

---

## Verification matrix

| Scenario | Expected |
|---|---|
| Unsigned project references DSoftStudio.Mediator | ✅ Works (always did) |
| Signed project references DSoftStudio.Mediator | ✅ Works (previously failed) |
| `dotnet test` all 312 tests | ✅ Pass |
| `sn -Tp` on all published DLLs | ✅ `PublicKeyToken ≠ null` |
| NuGet package install in signed project | ✅ No compilation errors |
| Source generator output in signed consumer project | ✅ Generates correctly |
| Native AOT publish | ✅ No regression |
