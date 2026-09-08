# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0-rc.1] — 2026-09-02

> Companions: `OpenTelemetry` 1.1.1-rc.1 · `HybridCache` 1.0.10-rc.1 · `FluentValidation` 1.0.10-rc.1.

Two changes in this release alter observable behavior. Both are described under **Changed**; read
those before upgrading.

### Added

- **A second `AddMediator(configure)` is now reported.** The overload runs your `configure` lambda on
  every call — it has to, or the lambda would be ignored — while the pipeline scan behind it returns
  early once it has run. A second call therefore registered components that no chain was ever rebuilt
  around: they either never ran, or a Singleton chain from the first scan captured them and the
  container refused to build under `ValidateScopes`. `ValidateMediatorHandlers()` now names them. The
  check reports what it OBSERVED, not what it inferred, so it stays quiet for a second bare
  `PrecompilePipelines()`, for a `configure` lambda that registers no component, and for a second call
  on a different service collection.

- **ADR-0065 — two-tier `Send` fast path.** The generator emits a concrete dispatch cache per
  (request, response) pair, plus an optional AGGRESSIVE armed holder that returns a Singleton handler
  directly when the pair provably has no pipeline. The aggressive tier is one-shot, stands down when
  the pair stops being eligible, and is guarded by a process-wide poison latch.
- **ADR-0066 — `Publish` fast path.** The same shape for notifications: a generated per-notification
  cache with an armed holder, and an unrolled dispatch body for groups of up to eight handlers.
- **Specialized behavior chains.** The generator predicts the exact ordered behavior chain per
  composition root and emits a purpose-built nest of typed links for it, verified positionally
  against the resolved instances at construction and falling back to the interface-typed adapter when
  the prediction does not match. Marginal cost per behavior link drops from 2.09 ns to 0.47 ns.
- **DSOFT009 — handler skipped because generated code cannot name it.** Discovery previously rejected
  only `file` types, so a `private` nested handler was registered anyway and the generated file failed
  to compile — a three-handler fixture produced 502 `CS0122` errors in files the user cannot edit.
  Unnameable handlers are now skipped and reported at their declaration, with the accessibility change
  that would include them.
- **XML documentation in the packages.** No project set `GenerateDocumentationFile`, so a consumer
  installing from NuGet saw nothing in IntelliSense. Every `Handle`/`Process` on the contracts you
  implement now documents its parameters, its return, and the shape to write — including why a
  synchronous handler should return a completed value rather than be marked `async`.
- **DSOFT010 — pipeline component registered after the pipeline scan.** The scan decides, per request
  type, whether a chain is built at all; a component registered afterwards never runs, with no
  exception and no diagnostic. The rule is deliberately narrow — same method body, same service
  collection, which is the Program.cs shape — because there is no compilation-wide ordering to
  consult. Everything it cannot see (another method, another assembly) is caught at startup by the
  `ValidateMediatorHandlers()` check above.
- **Orphaned-component detection in `ValidateMediatorHandlers()`.** The generated validator now
  reports pipeline components that are registered but can never run, and Transient components captured
  by a chain whose lifetime was fixed before they were registered. This is the only check that sees
  the case across methods, files and assemblies, because it inspects the built container rather than
  one syntax tree.
- **ADR-0007 — notification observation port.** `IMediatorNotificationObserver` lets a tracing bridge
  or a profiler watch a publish without replacing `INotificationPublisher`, which was the only option
  before and was never neutral: it disarmed the generated dispatch, resolved handlers from the
  container instead of the compile-time table — so a handler the generator could not see went from
  skipped to invoked — and handed back a different instance for a stateful singleton. The core calls
  the observer and the observer substitutes nothing. Registering one is knowable before the container
  is built, so an application that registers none pays nothing. Every registered observer runs, not
  just the first, and a publish reports how many subscribers it resolved before the first one starts.
- **Per-request-pair registration for the companion packages.** `AddMediatorHybridCache<TRequest,
  TResponse>()` and `AddMediatorFluentValidation<TRequest, TResponse>()` register the behavior closed
  over one pair instead of as an open generic. The open form joins the pipeline of every request in
  the application, so a request that never caches or validates has a chain built for it rather than
  being dispatched straight to its handler; the closed form leaves every other request untouched. The
  caching overload constrains `TRequest` to `ICachedRequest`, so registering a request that never
  opted in does not compile. The two forms are alternatives: with the open registration present, the
  closed call stands down.
- **DSOFT010 now sees the registrations it most needs to.** The rule reports a pipeline component
  registered after the scan that decides whether a chain is built, but it could not see the companion
  packages — their descriptor is added inside their own extension method, in another assembly, so
  nothing at the call site names a component interface — nor the
  `TryAddEnumerable(ServiceDescriptor.Singleton(...))` form, which is what those packages now use. It
  recognises the three first-party registration methods by name, gated on the namespace so another
  vendor's same-named method cannot trip it, and reads through `ServiceDescriptor`'s factories.
- **Benchmarks.** Multi-targeted `net10.0`/`net11.0` with `runtime-async` on net11, a behavior-count
  scaling suite, `run-all-benchmarks.cmd`, and `compare-runs.ps1`, which compares two runs by overhead
  above each suite's own baseline so machine drift cancels out.

### Changed

- **Pipeline components registered through `MediatorBuilder` now default to `Scoped`, not
  `Transient`.** `AddOpenBehavior`, `AddStreamBehavior`, `AddRequestPreProcessor`,
  `AddRequestPostProcessor` and `AddRequestExceptionHandler` are affected; `AddDispatchObserver` keeps
  its Singleton default. One Transient component registers the request's whole chain as Transient, so
  every dispatch re-resolved and re-linked it instead of reusing the one already built for the scope —
  which put a caller who expressed no opinion on the slow path and gave them nothing for it. Scoped is
  the longest lifetime that is safe without inspecting the component's constructor. **This is a
  behavior change:** a Scoped component is shared by every dispatch in the scope, including concurrent
  ones, so a component holding per-dispatch state or a non-thread-safe field — a `Stopwatch` started
  before the call and read after it — must now be registered `Transient` explicitly. Passing a lifetime
  yourself is unaffected; only the unstated default moved.
- **A Transient handler now makes its pipeline chain Transient.** The chain's constructor consumes the
  handler, so its lifetime has to constrain the chain — and `HandlerLifetimeOptimizer` leaves a handler
  Transient exactly when a dependency of its own is transient, that is, when a fresh instance per
  resolve is the whole point. Folding it only far enough to stop the chain being Singleton left a
  cacheable Scoped chain that constructed the handler once per scope and shared the very dependency the
  optimizer had just refused to share. The same fold now also reads the stream handler and any
  registered `IMediatorDispatchObserver`, both of which the chain likewise consumes. **This is a
  behavior change** for an application whose handler is genuinely Transient: its chain is now rebuilt
  per dispatch, which is what its registration asked for.
- **A pipeline component registered after the scan is reported where it used to be silent.** The old
  Transient default masked this: it had already dragged the chain to Transient, so a late Transient
  component happened to be per-dispatch by accident of the slow path and the validator stayed quiet.
  With a cacheable chain the component really is constructed once and shared, and
  `ValidateMediatorHandlers()` says so. The error is true and names both fixes; the registration order
  it asks for has always been the documented contract.

- **`ParallelNotificationPublisher` now actually runs handlers in parallel.** It used to invoke each
  handler inline on the calling thread and await the resulting tasks together, so handlers that
  complete synchronously — the style `INotificationHandler` recommends — never overlapped at all. Each
  handler is now queued to the thread pool. **This is a behavior change:** handlers under this
  publisher must be safe to run alongside each other, where before synchronous ones were serialized by
  accident, and it costs one thread-pool work item per handler. The default dispatch path is
  unaffected and pays neither.
- **`IRequestExceptionHandler` now covers pre-processors.** The guard used to wrap only the behavior
  chain and the terminal handler, so a throw from an `IRequestPreProcessor` — a validation or
  authorization check, exactly what an exception handler exists to translate — was never shown to one.
  The pre-processor stage now runs inside the guard. **This is a behavior change:** a pre-processor
  exception that escaped to the caller before may now be suppressed by a handler that returns a
  response. Post-processors remain deliberately outside the guard: a response already exists by the
  time they run, so substituting another would contradict the ones that already observed the first.
- **Generated source no longer depends on the build machine's locale.** Every ordering site in the
  generators sorted type-name strings through `Comparer<string>.Default`, which is culture-sensitive.
  Under `da-DK` "Aa" collates as "Å" and sorts after "Z", so the same commit produced a different
  dispatch table — and different IL — on a Danish machine than on an English one, defeating
  deterministic-build verification. All 25 sites now pass `StringComparer.Ordinal`.
- **One chain-resolution protocol instead of seven copies.** Whether a chain may be cached per
  (thread, provider) was hand-written as a ternary at four `Send` sites and three stream sites. It now
  lives inside `PipelineChainCache.Resolve` and `StreamPipelineChainCache.Resolve` — which is also
  where the stream one was missing it entirely, caching unconditionally and pinning the first instance
  of a Transient stream chain.
- **The dispatch decision collapsed into one pre-resolved reference.** `PipelineChainHandler` resolves
  the observer check and the three-way pipeline mode into a single field at construction, so the
  common shape is one read and one branch.

### Fixed

- **Handlers that inject a logger were never promoted.** `HandlerLifetimeOptimizer` indexed the
  registered lifetimes by exact service type, and `AddLogging()` registers `ILogger<>` OPEN. A handler
  depending on `ILogger<THandler>` looked up the CLOSED type, missed, and counted as having an
  unregistered dependency — which pins the handler at Transient. Since injecting a logger is the
  commonest thing a handler does, the optimizer's central promise was quietly off for most real
  handlers, and through the chain-lifetime fold their whole pipeline chain stayed non-cacheable with
  them. The lookup now falls back to the open-generic registration, as the container does, and as
  `DispatchCacheability` already did. `IOptions<T>` and every other openly-registered framework service
  are fixed by the same change; a closed registration still wins over the open one behind it.
  The services the container provides without any descriptor are answered directly for the same
  reason: `IServiceProvider` is `Scoped` — it is the scope that asked and lives exactly as long — and
  `IServiceScopeFactory` is `Singleton`, since one rooted factory serves every scope.
- **A late Transient behavior could be hidden by a later registration.** The validator asked whether
  the behavior service type was cacheable, and that answer is last-wins — the reading the container
  uses for a service it resolves singly. `IPipelineBehavior<,>` is not one of those: several
  descriptors coexist and every one of them runs, so a Scoped registration made after a Transient one
  silenced the rule about a behavior that really was constructed once and shared. It now asks whether
  ANY registration for the type is Transient. The dispatch caches are untouched — they only ever ask
  about service types that resolve singly, where last-wins is correct.
- **DSOFT010 could not see the fluent registration style at all.** The rule pairs a component with a
  scan of the SAME service collection, by symbol, and the receiver of a chained call
  (`services.AddMediator().PrecompilePipelines()`) is the previous invocation rather than a symbol — so
  the scan was never recorded and nothing could be reported against it. Registrations made through a
  `MediatorBuilder` had the same problem from the other side: their receiver is the builder, never the
  collection. Both are now resolved back to the collection, including the builder handed to a
  `configure` lambda, which is the shape a late module uses.
- **DSOFT010 reported components against scans that do not govern them.** A stream behavior registered
  after `PrecompileNotifications()` but before its own `PrecompileStreams()` is correctly ordered, and
  the rule flagged it anyway — a false positive in a diagnostic that users build with
  `TreatWarningsAsErrors`. A component is now paired only with the scan that would have built its
  chain. `PrecompileNotifications()` governs no pipeline component and pairs with none.

- **FluentValidation reported every failure more than once.** One `ValidationContext` was shared by
  every validator, and FluentValidation accumulates failures in the context it is handed — so the
  second validator returned its own failures plus the first's, the third all three, and so on. Three
  broken rules across two validators produced five failures, with two messages duplicated, an
  `ErrorsByProperty` entry carrying the same string twice, and a message reading "Validation failed
  with 5 errors". **The failure count and contents change**: anything asserting on them, or rendering
  them into a `ValidationProblemDetails`, sees the correct list now.
- **A cached handler ran without the caller's execution context.** `HybridCache` dispatches the
  factory through `ThreadPool.UnsafeQueueUserWorkItem` when the caller's token can be cancelled, and
  that overload captures no `ExecutionContext` — so on a cache miss the handler saw
  `Activity.Current` and `IHttpContextAccessor.HttpContext` as null. Traces detached from the
  request, and a handler resolving its tenant or user from ambient state computed the wrong answer,
  which was then cached for the whole entry lifetime. The behavior now restores the caller's context
  around the downstream call, without giving up the caller's ability to cancel.
- **Installing a companion made every dispatch in the application rebuild its pipeline chain.**
  `HybridCache`, `FluentValidation` and the metrics and stream behaviors in `OpenTelemetry` all
  registered their pipeline component as `Transient`. One transient component is enough for the
  generated registration to mark the whole chain transient and skip the cacheable flag, so the chain
  was re-resolved and re-linked on every request — measured at 107 ns and 200 B per dispatch against
  80 ns and 24 B when cached. In OpenTelemetry it applied to streams under the default options, so
  merely enabling tracing was enough. All are now Singleton, or Scoped where the component's own
  dependencies are scoped.
- **Registering a companion twice put its behavior in every chain twice.** A shared library and the
  host application both calling `AddMediatorHybridCache()` nested the caching behavior inside itself,
  so the outer lookup re-entered the inner one on the same key while the first call was still in
  flight; the FluentValidation equivalent ran every validator twice. Both now register through
  `TryAddEnumerable`.
- **The HybridCache package README documented an API that does not exist.** Its quick start used a
  generic `ICachedRequest<T>` with an `Expiration` property and an `AddMediatorCaching()` method,
  none of which are real, and claimed cache keys were derived from the request type and its property
  values when they are supplied verbatim by the caller. Every symbol in it failed to compile. The
  FluentValidation README's error-handling sample used a property that does not exist either. Both
  are rewritten against the code, with every sample compiled.

- **Publishing a notification with no subscribers no longer depends on how the call is written.**
  `Publish(object)` threw `InvalidOperationException` where the generic overload was a no-op, so the
  same event published from a domain-event or outbox loop failed while the direct call succeeded.
  Dispatch plans are built from handlers, so a notification nobody subscribes to has no table entry —
  the ordinary state of an event nothing listens for yet, not a wiring fault, and the old message
  ("Ensure PrecompileNotifications() is called") sent people to debug a registration that was fine.
  Both forms are now a no-op. The two errors worth keeping still throw: publishing something that is
  not an `INotification`, and publishing before `PrecompileNotifications()` has run, which now says
  exactly that. Present since 1.3.0.

- **The dispatch caches now honour the registered lifetime.** Every provider-keyed `[ThreadStatic]`
  cache stored whatever it resolved, keyed only on the provider. That is right for Singleton and
  Scoped and wrong for Transient: three `Send` calls of a handler with a Transient dependency
  constructed that dependency once and shared it, while raw DI on the same provider handed back
  distinct instances — the opposite of what the library's own documentation promised. Cacheability is
  now asked of the container.
- **Each container keeps its own lifetime answer.** The cacheability verdict was a process-global
  static per closed pair, so the first container to register a Scoped chain latched it on for every
  container after it. It is now captured per provider by a Singleton `DispatchLifetimeSnapshot`, which
  is the only lifetime that is built once per provider and shared with all of its scopes.
- **The lifetime map keeps the collection it reads.** It held the `IServiceCollection` weakly, but a
  `ServiceCollection` is a local in startup code — any collection between `BuildServiceProvider` and
  the first request took the target away, the map settled on an empty snapshot, and every
  provider-keyed cache in the library silently stopped caching for that container, permanently and
  non-deterministically.
- **Disposing a scope releases the dispatch caches that held it.** A disposed scope, and every scoped
  instance it resolved, stayed reachable in thread-local state until the same thread happened to
  dispatch the same request type against a different provider — for a rarely-served request type, or a
  pool thread that moves on, never.
- **An armed holder stands down when its pair stops being eligible.** Losing eligibility was a no-op
  once the state was `Attempted`, so a pair that had already armed and then had a behavior, processor,
  exception handler or observer appear kept dispatching straight to the handler: every one of those
  components silently never ran.
- **The chain predictor uses the generator-wide nullable display format.** Its private copy omitted
  the nullable annotation, so a pair with a nullable response emitted a `CS8631` mismatch and never
  matched a closed registration — no error, just no speedup.
- **One predicted chain per composition root, not per file**, and duplicate predictions are dropped.
- **Null-provider guard in the resolve caches.** `ReferenceEquals(null, null)` is true, so a null
  provider matched the initial null and returned a "cached" null, surfacing later as an unrelated
  `NullReferenceException`. The stream caches also failed two different ways from the same state; they
  now throw the same message, which names the missing `PrecompileStreams()` call.
- **`ParallelNotificationPublisher` no longer abandons started handlers.** A handler that threw
  *synchronously* escaped the start loop, so the handlers after it never ran and the tasks already
  started were never awaited — their failures surfaced later as `UnobservedTaskException`.
- **Documentation corrected against the implementation.** Several published claims did not match the
  code: notification handlers were documented as running in registration order (the order is by
  handler type name), stream dispatch was documented as fully deferred (the handler and behavior chain
  are resolved when `CreateStream` is called), `SequentialNotificationPublisher` was described as the
  default (no publisher is registered by default), and `PrecompilePipelines()` was described as
  capturing the component list (it records only whether a chain is needed, and freezes its lifetime).

### Removed

- **`BehaviorLinkRegistry`** — a middle chain-construction tier no generator ever populated. Its
  dictionary was null in every process and `Link()` fell through to the interface-typed adapter on
  every call. Per-pair generic static types: 6 → 5.

## [1.3.0] — 2026-07-08

> Companions: `OpenTelemetry` 1.1.0 · `HybridCache` 1.0.9 · `FluentValidation` 1.0.9.

### Added

- **`IPipelineHandlerTypeAccessor` (Abstractions)** — exposes the concrete request/stream handler type at the tail of the pipeline chain to an outermost pipeline behavior, without resolving or instantiating it. A behavior is open-generic and may serve many handlers; the correct one for a given request is only knowable by walking the chain it was handed as `next`. The internal chain adapters (`BehaviorHandlerAdapter`, `StreamBehaviorHandlerAdapter`) implement the interface; a behavior reads the terminal handler via `next is IPipelineHandlerTypeAccessor`. Enables tracing/diagnostics to tag the concrete handler.
- **DSOFT008 — missing handler registration** — new compile-time diagnostic (Warning) that flags a parameterless `AddMediator()` when handlers exist in the compilation but nothing anywhere registers them (no builder overload, no `RegisterMediatorHandlers()`, no manual `AddTransient<IRequestHandler<,>>`). Detection is **compilation-wide** (reported from a `CompilationEndAction`), so splitting `AddMediator()` and the handler registration across different methods is not a false positive.
- **OpenTelemetry: `mediator.handler.type` on request and stream spans** — the bridge now tags the concrete handler type on request-send and stream spans (it already did so for notification-handler spans), so an imported OTLP/Jaeger trace maps each span to its handler source and renders HTTP/DB child spans as dependencies under it. The handler type is read through the new `IPipelineHandlerTypeAccessor` — it is never resolved or instantiated.

### Changed

- **DSOFT007 converted to a `DiagnosticAnalyzer`** — runs after source generators, so it correctly sees the generated `RegisterMediatorHandlers()` / builder-overload registrations that the prior generator-based diagnostic could not (DSOFT007 was silently inert; DSOFT008 false-positived).
- **DSOFT006 location widened** to the full type header so the IDE offers the ConvertToCqrs lightbulb when hovering the offending `IRequest<T>` base type, not just the type name (Info severity unchanged).
- **Dependency bumps** — `Microsoft.Extensions.DependencyInjection.Abstractions` → 10.0.9 and `Microsoft.Bcl.AsyncInterfaces` → 10.0.9 (core/abstractions, .NET 10 servicing band); companion `OpenTelemetry` → 1.16.0; companion `Microsoft.Extensions.Caching.Hybrid` → 10.7.0. `Microsoft.CodeAnalysis.CSharp` is intentionally kept at 4.12.0 — the generator's referenced Roslyn version is the minimum compiler-host a consumer needs, so raising it would break consumers on older SDK/VS.

### Removed

- **`PipelineBuilder` and `RequestDispatch<,>.Pipeline` / `TryInitialize`** — vestigial dead dispatch state. The generator populated a per-request-type pipeline delegate that nothing ever invoked: `Send`, the interceptors and `Send(object)` all dispatch through `RequestDispatch.HasPipelineChain` + the ThreadStatic `PipelineChainCache` / `HandlerCache` (the stream side still uses `StreamDispatch.Pipeline`; the request side left it vestigial). `RequestDispatch<,>` is `[EditorBrowsable(Never)]` infrastructure, and `PipelineBuilder` was public but only ever intended for generated code that no longer uses it. Removal saves one delegate allocation per request type at startup with no behavior or hot-path change. (If you referenced `PipelineBuilder.Build<,>()` directly — unsupported — resolve `PipelineChainHandler<,>` from DI instead.)

### Fixed

- **Open-generic dispatch call sites no longer break the build** — the `Send` / `Publish` / `CreateStream` interceptor generators attempted to intercept call sites whose type arguments are open type parameters (e.g. a generic dispatch wrapper `Dispatch<TReq, TResp>(req) => sender.Send<TReq, TResp>(req)`) and emitted code referencing the unbound parameters → `CS0246`. A single `[InterceptsLocation]` cannot stand in for a call site instantiated across many type arguments, so such sites are now skipped and dispatch through the runtime `Mediator.Send` / `Publish` / `CreateStream`.

### Security

- **Scriban 7.0.3 → 7.2.4** in the benchmarks project (dev-only, not shipped) — resolves [GHSA-24c8-4792-22hx](https://github.com/advisories/GHSA-24c8-4792-22hx) (high severity).

## [1.2.0] — 2026-04-12

### Added

- **`MediatorBuilder` fluent configuration API** — New `AddMediator(Action<MediatorBuilder> configure)` overload (source-generated) that provides a single entry point for mediator registration. The builder automatically calls `RegisterMediatorHandlers()`, applies the user's configuration, calls `RegisterPipelineChains()`, and freezes the dispatch tables — replacing the previous 3-step manual setup. Six fluent methods:
  - `AddOpenBehavior(Type, ServiceLifetime)` — registers open-generic `IPipelineBehavior<,>` (e.g., `typeof(LoggingBehavior<,>)`)
  - `AddStreamBehavior<T>(ServiceLifetime)` — registers closed `IStreamPipelineBehavior<TRequest, TResponse>`
  - `AddRequestPreProcessor<T>(ServiceLifetime)` — registers `IRequestPreProcessor<TRequest>`
  - `AddRequestPostProcessor<T>(ServiceLifetime)` — registers `IRequestPostProcessor<TRequest, TResponse>`
  - `AddRequestExceptionHandler<T>(ServiceLifetime)` — registers `IRequestExceptionHandler<TRequest, TResponse>`
  - `AddParallelNotificationPublisher()` — replaces sequential publisher with `Task.WhenAll` parallel dispatch

  All generic methods are annotated with `[DynamicallyAccessedMembers]` for Native AOT safety. `Services` property is publicly accessible for advanced DI scenarios within the builder callback.

  ```csharp
  services.AddMediator(builder =>
  {
      builder.AddOpenBehavior(typeof(LoggingBehavior<,>));
      builder.AddRequestPreProcessor<ValidationPreProcessor>();
      builder.AddParallelNotificationPublisher();
  });
  ```

- **Strong naming** — All 5 assemblies (`DSoftStudio.Mediator`, `DSoftStudio.Mediator.Abstractions`, `DSoftStudio.Mediator.FluentValidation`, `DSoftStudio.Mediator.HybridCache`, `DSoftStudio.Mediator.OpenTelemetry`) are now signed with `PublicKeyToken=6c7e753832e8eb05`. Enables installation in GAC, use from other strong-named assemblies, and tamper detection. Key file: `DSoftStudio.Mediator.snk` with `InternalsVisibleTo` attributes updated across all projects. See [ADR-0003](docs/mediator/adr/0003-strong-naming.md).
- **DSOFT007: Mixed registration API detection** — New compile-time diagnostic (Warning) that detects when `RegisterMediatorHandlers()` or `PrecompilePipelines()` are called alongside `AddMediator(Action<MediatorBuilder>)`. The builder overload already performs these operations internally — calling them separately causes redundant registrations. The diagnostic identifies the specific redundant call and explains what the builder handles automatically.
- **Runtime idempotency guards** — Source-generated `RegisterMediatorHandlers()` and `RegisterPipelineChains()` now detect duplicate invocations via a per-`IServiceCollection` sentinel pattern (private `__Sentinel` / `__PipelineSentinel` marker types registered as singletons). If the sentinel is already present, the method returns immediately — preventing double handler/pipeline registration when `AddMediator(configure)` is used alongside legacy calls. The sentinel approach was chosen over `static bool` guards to preserve test isolation across parallel test classes with independent `ServiceCollection` instances.

### Changed

- **Companion packages bumped to 1.0.8** — `DSoftStudio.Mediator.FluentValidation`, `DSoftStudio.Mediator.HybridCache`, `DSoftStudio.Mediator.OpenTelemetry` updated to depend on `DSoftStudio.Mediator >= 1.2.0`. All companion assemblies are now strong-named.
- **Analyzer release tracking** — DSOFT007 shipped in `AnalyzerReleases.Shipped.md` under Release 1.2.0.

### Architecture Decisions Recorded

- **ADR-0003: Strong Naming** — Accepted. All published assemblies signed with a committed `.snk` key for enterprise compatibility, GAC installation, and `InternalsVisibleTo` with public key verification. See [`docs/mediator/adr/0003-strong-naming.md`](docs/mediator/adr/0003-strong-naming.md).

---

## [1.1.8] — 2026-03-23

### Added

- **DSOFT001: Missing handler detection** — New compile-time diagnostic (Warning) that detects `IRequest<T>`, `ICommand<T>`, and `IQuery<T>` types with no corresponding `IRequestHandler<TRequest, TResponse>` implementation. Works across project boundaries via `ReferencedAssemblyScanner` and also recognizes self-handling requests (`static Execute`). Catches orphan request types before runtime `InvalidOperationException`.
- **DSOFT006: CQRS semantic analyzer** — New compile-time diagnostic (Info) that suggests using `ICommand<T>` or `IQuery<T>` instead of `IRequest<T>` for improved intent clarity. Zero runtime cost — purely informational at build time.
- **Nullable response type support** — Source generators now emit fully nullable-qualified type names (`NullableFullyQualifiedFormat`) for all handler registrations, interceptors, and typed extensions. Types like `IRequest<string?>`, `IRequest<List<int?>?>`, and `IRequest<(string? Name, int? Age)?>` are correctly propagated through the entire pipeline.
- **Diagnostic integration tests** — Comprehensive Roslyn in-memory test suites for all compile-time diagnostics: `DependencyInjectionDiagnosticTests` (12 tests covering DSOFT001/002/003), `ReferencedAssemblyDiagnosticTests` (6 tests covering DSOFT001/005 with 3-assembly pattern), `CqrsSemanticAnalyzerTests` (10 tests for DSOFT006).
- **Nullable integration tests** — 14 same-assembly tests (`NullableResponseTests`) and 4 cross-assembly tests (`NullableCrossAssemblyTests`) validating nullable type propagation through handlers, interceptors, and cross-project discovery.
- **Enterprise integration tests** — 48 tests across 14 test classes covering multi-project discovery, DI lifetime validation, deep pipeline (6 behaviors), 2000-parallel concurrency, Native AOT precompilation, expression tree safety, complex generics, runtime vs compile-time dispatch, background service patterns, stress testing (5000 sequential + 100 parallel streams), failure injection with retry, allocation regression, timeout/deadlock detection, and chaos testing. See [Production Validation](docs/mediator/architecture/production-validation.md).
- **Production validation documentation** — New `docs/mediator/architecture/production-validation.md` page documenting all 48 enterprise integration tests organized by category with direct links to source.
- **`NullableCrossAssemblyDiscoveryTests`** — 2 Roslyn in-memory regression tests covering both cross-assembly discovery paths: Phase 1 (attribute-based, where `typeof()` strips nullable — the bug path) and Phase 2 (type-based, where `AllInterfaces` preserves nullable from PE metadata). Both verify generated DI registrations contain the correct `global::User?>` annotation.

### Fixed

- **Nullable type names lost in generated code** — All 6 source generators (`DependencyInjectionGenerator`, `SendInterceptorGenerator`, `PublishInterceptorGenerator`, `StreamInterceptorGenerator`, `CqrsSemanticAnalyzer`, `ReferencedAssemblyScanner`) now use `NullableFullyQualifiedFormat` instead of `SymbolDisplayFormat.FullyQualifiedFormat`, preserving `?` annotations in all emitted code.
- **Benchmarks documentation link** — Fixed broken relative link in `docs/mediator/benchmarks.md`.
- **CS8631 nullable constraint mismatch in cross-assembly handler discovery** — `typeof()` inside `[MediatorHandlerRegistration]` assembly attributes strips nullable reference type annotations at the IL level — e.g., `typeof(IRequestHandler<GetUser, User?>)` becomes `typeof(IRequestHandler<GetUser, User>)` in metadata. This caused CS8631 warnings and false DSOFT001 diagnostics when a test project referenced a project containing handlers with nullable response types. The fix in `ReferencedAssemblyScanner.CollectHandlersFromAttributes()` re-resolves the service type from `implType.AllInterfaces` (which preserves nullable annotations via PE metadata `NullableAttribute`), using `SymbolEqualityComparer.Default` to match ignoring nullable differences.

### Changed

- **Enterprise test hardening** — Replaced static `FlakeyPingHandler.FailuresRemaining` with injectable `FlakeyState` class, introduced `IChaosRandom` interface for deterministic chaos tests, added `[Trait("Category", "NonDeterministic")]` to allocation tests, increased timeout margins with `Debugger.IsAttached` awareness, and strengthened expression tree tests with `Compile()` + `MethodCallExpression` verification.
- **Analyzer release tracking** — DSOFT002–DSOFT006 moved from `AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md` under Release 1.1.8-rc.1.

---

## [1.1.7] — 2026-03-22

### Added

- **Realistic pipeline benchmarks** — New enterprise-realistic benchmark suite measuring all 4 libraries (DSoft, MediatR, DispatchR, Mediator SG) through a production-representative pipeline: Validation → Logging → Metrics → async database write with 3 pipeline behaviors and full DI. Each library has its own `DirectCall_WithPipeline` fair baseline running in the same isolated BenchmarkDotNet process. Shared infrastructure: `CreateOrderCommand` message types, `FakeOrderRepository` with simulated async I/O.
- **Per-library DirectCall baselines** — Each library's realistic pipeline benchmark now includes its own direct-call baseline measured in the same isolated process, eliminating cross-process variance from comparisons.
- **Send(object) / Publish(object) benchmarks** — New isolated benchmark classes for all 4 libraries (`DSoftSendObjectBenchmarks`, `DSoftPublishObjectBenchmarks`, `MediatRSendObjectBenchmarks`, `MediatRPublishObjectBenchmarks`, `DispatchRPublishObjectBenchmarks`, `MediatorSGSendObjectBenchmarks`, `MediatorSGPublishObjectBenchmarks`) measuring runtime-typed dispatch overhead vs generic dispatch.
- **Typed extension vs Mediator SG benchmark** — New `DSoftTypedExtensionVsMediatorSGBenchmarks` class comparing DSoft source-generated typed extensions against martinothamar/Mediator SG dispatch in the same isolated process.
- **Packaging regression tests** — New `Packaging/BuildTransitivePropsTests` verifying that both `build/` and `buildTransitive/` props files contain `InterceptorsNamespaces`, `InterceptorsPreviewNamespaces`, `DSoftMediatorSuppressInterceptors`, and stay in sync. New `Packaging/InterceptorNamespaceCompilationTests` using Roslyn in-memory compilation to reproduce and guard against CS9137 regressions.

### Fixed

- **CS9137 when consuming the NuGet package** — The `buildTransitive/DSoftStudio.Mediator.props` file was missing `InterceptorsNamespaces` / `InterceptorsPreviewNamespaces` configuration. When NuGet sees both `build/` and `buildTransitive/` with the same filename, `buildTransitive/` takes priority even for direct `PackageReference` consumers. The source generator emitted interceptors but the compiler rejected them because the namespace was not enabled. The transitive props file now mirrors `build/` with full interceptor namespace configuration and `DSoftMediatorSuppressInterceptors` support.
- **MediatorSG benchmark crash** — Added `FakeOrderRepository` singleton registration to `MediatorSGHelper.AddMediatorSG()` to prevent the eager handler resolution from failing during realistic pipeline benchmarks.

### Changed

- **README rewrite** — Complete production-grade rewrite focused on adoption and enterprise credibility. New hero narrative ("A mediator with zero structural cost"), realistic pipeline proof table with per-library DirectCall baselines, GC pressure / tail latency / pipeline depth analysis, "When to use this" positioning, feature comparison matrix, execution model diagram, ecosystem section, and "Your mediator should not be part of your performance budget" closing. MediatR comparison updated to v14.1.
- **Shared `InterceptorHelpers` dispatch body builder** — `AppendSendDispatchBody` extracted to a single static method shared by `SendInterceptorGenerator` (interceptors) and `MediatorExtensionsGenerator` (typed extensions). Ensures dispatch logic is defined once, with `isRelease` parameter for branchless castclass (Release interceptors) vs isinst + virtual-dispatch fallback (typed extensions and Debug interceptors).
- **Typed extensions use defensive dispatch** — `MediatorExtensionsGenerator` now emits `isinst` + virtual-dispatch fallback (never branchless castclass), ensuring typed extensions work correctly in test projects, mocking scenarios, and `dotnet test -c Release` CI pipelines regardless of interceptor suppression.
- **Benchmark suite restructured** — Deleted legacy combined benchmark classes (`ColdStartBenchmarks`, `ConcurrencyBenchmarks`, `PublishBenchmarks`, `SendBenchmarks`, `SendNoBehaviorsBenchmarks`, `StreamBenchmarks`) and `run-all-benchmarks.cmd`. Each library now has isolated per-category benchmark classes under `Benchmarks/{Library}/` directories.
- **Benchmark report generator** — `generate-benchmarks-md.ps1` now filters Direct-only rows from "All Libraries" combined sections, removes empty separator rows, and adds library-group separators with dynamic column-width matching for cleaner combined tables.
- **Benchmark run scripts** — All 4 `run-*.cmd` files updated to include realistic pipeline and Send(object)/Publish(object) benchmark classes.
- **Companion packages bumped to 1.0.5** — `DSoftStudio.Mediator.FluentValidation`, `DSoftStudio.Mediator.HybridCache`, `DSoftStudio.Mediator.OpenTelemetry` updated to depend on `DSoftStudio.Mediator >= 1.1.7`.

---

## [1.1.6] — 2026-03-21

### Added

- **Native AOT safety integration tests** — New `NativeAotSafetyTests` suite (7 tests) validating that `PrecompilePipelines()` correctly replaces open-generic `IPipelineBehavior<,>` descriptors with closed-generic versions. Covers end-to-end dispatch (Unit/int/bool), multiple behaviors ordering, zero open-generic assertion, lifetime preservation (Transient/Scoped/Singleton), and idempotency.

### Fixed

- **CS8600 nullable conversion in `MockDetectionAnalyzer`** — The `DetectMockingLibrary` call site now declares `string?` and uses `is null` pattern matching, eliminating the CS8600 warning without sentinel values.
- **CS8603 possible null return in `MockDetectionAnalyzer`** — `DetectMockingLibrary` return type corrected to `string?` to match its nullable contract.
- **CS8625 null-to-non-nullable in `ReferencedAssemblyScanner`** — `GetAllExternalHandlers` parameter changed to `List<SkippedHandlerInfo>?` to accurately reflect its optional nature.
- **CS8765 nullable parameter mismatch in `MockDetectionAnalyzerTests`** — `TryGetValue` overrides now match the base `AnalyzerConfigOptions` signature (`out string value`) using `null!` for the false-return pattern.
- **xUnit1051 across all test projects** — All `CancellationToken.None` / omitted `CancellationToken` arguments in test methods replaced with `TestContext.Current.CancellationToken` for responsive test cancellation under xUnit v3.

### Changed

- **README rewrite** — Complete restructure with new sections: Execution Model (ASCII pipeline diagram), When to Use This (explicit DSoft vs MediatR positioning), Ecosystem (category-labeled companion packages). Feature Comparison table empirically verified against martinothamar/Mediator 3.0.1, DispatchR 2.1.1, and MediatR 12.4.1 with live test projects. DispatchR corrected to ✅ for exact-type notification dispatch. Mediator (SG) Native AOT compatibility confirmed via `dotnet publish` AOT + native binary execution.
- **Design-time build cache cleanup** — Stale `.dtbcache.v2` causing IntelliSense false positives (CS0246/CS0518) documented and resolved via cache invalidation.
- **Companion packages bumped to 1.0.4** — `DSoftStudio.Mediator.FluentValidation`, `DSoftStudio.Mediator.HybridCache`, `DSoftStudio.Mediator.OpenTelemetry` updated to depend on `DSoftStudio.Mediator >= 1.1.6`.

---

## [1.1.5] — 2026-03-20

### Added

- **`DSoftStudio.Mediator.Abstractions` NuGet package** — Contracts (interfaces and base types) are now published as a separate package. Domain, application-core, and test projects can reference only `DSoftStudio.Mediator.Abstractions` to get `ISender`, `IPublisher`, `IMediator`, `IRequest<T>`, `INotification`, and related abstractions **without** pulling in the runtime or source generators. This is the recommended pattern for unit-testing with mocking frameworks (Moq, NSubstitute, etc.) since no interceptors are active.
- **DSOFT004: Mock detection analyzer** — New compile-time diagnostic (Warning) that detects when a project references both the source generators and a mocking framework (Moq, NSubstitute, FakeItEasy). Recommends referencing only `DSoftStudio.Mediator.Abstractions` in test projects for clean mock isolation.
- **`DSoftMediatorSuppressInterceptors` MSBuild kill switch** — Set `<DSoftMediatorSuppressInterceptors>true</DSoftMediatorSuppressInterceptors>` in a project to completely disable interceptor generation. Useful for test projects or environments where interceptors are undesirable.
- **`NotificationPublisherFlag`** — Write-once volatile flag that allows the runtime `Publish` path to skip unnecessary overhead when no notification publishers are registered.
- **Cross-project mocking sample** — New `samples/cross-project-mocking/` demonstrates the recommended architecture: `Host` (runtime + generators), `Host.Application` (abstractions only), `Host.Tests` (mocks against abstractions).
- **DSOFT005: Internal handler skipped analyzer** — New compile-time diagnostic (Warning) reported for every handler discovered in a referenced assembly but skipped because it is not accessible. The message includes the handler type name and the assembly it belongs to, so library authors can decide whether to make the handler `public` or add an `InternalsVisibleTo` attribute.
- **CS0122 regression tests** — New test project (`DSoftStudio.Mediator.ModularMonolith.Tests`) with compile-time guard (`WarningsAsErrors=CS0122`) ensuring the internal-handler accessibility fix cannot regress.

### Fixed

- **DSOFT004 not respected in transitive projects** — `DSoftMediatorSuppressInterceptors=true` was silently ignored when the project consumed DSoftStudio.Mediator transitively (e.g. `Host.Tests` ? `Host` via `ProjectReference`). Root cause: the `CompilerVisibleProperty` declaration lived only in the `build/` NuGet folder, which is **not** transitive. Added a `buildTransitive/DSoftStudio.Mediator.props` file containing just the `CompilerVisibleProperty` declaration so the source generator can read the suppress flag in any downstream project. Interceptor namespaces are intentionally **not** included in the transitive props.
- **Interceptors rewriting call sites inside expression tree lambdas** — The source generators (`SendInterceptorGenerator`, `PublishInterceptorGenerator`, `StreamInterceptorGenerator`) no longer rewrite `Send`, `Publish`, or `CreateStream` calls that appear inside expression tree lambdas (e.g. Moq `Setup()` / `Verify()`). A new `IsInsideExpressionTreeLambda` helper walks the syntax tree and checks the lambda's `ConvertedType` against `System.Linq.Expressions.Expression<T>`, skipping those call sites. Direct invocations outside expression trees continue to be intercepted as before. ([#2](https://github.com/DSoftStudio/Mediator/issues/2))
- **Flaky parallel notification tests** — `ParallelNotificationPublisherTests` and `PipelineGcLeakTests` stabilized with deterministic synchronization to eliminate intermittent CI failures.
- **Modular monolith CS0122** — The source generator no longer emits DI registrations for `internal` handlers discovered in referenced assemblies, which previously caused `CS0122` at compile time. Accessibility is now validated during generation, and `InternalsVisibleTo` is respected. A new **DSOFT005** warning identifies every skipped handler.

### Changed

- **Branchless interceptor dispatch (Release builds)** — Interceptor generators now emit `OptimizationLevel`-conditional code: Release builds use `castclass` (branchless, ~0.36 ns saving via GDV), Debug builds use `isinst` + null-check for mock-framework compatibility. Hot-path `Send` is now **1.05×** vs raw handler.
- **Publish optimization** — `Publish` dispatch leverages `NotificationPublisherFlag` to skip publisher resolution when none are registered. Overhead dropped from **2.19×** to **1.07–1.11×** vs raw handler.
- **NuGet package split** — `DSoftStudio.Mediator` now declares a public NuGet dependency on `DSoftStudio.Mediator.Abstractions` (previously the Abstractions DLL was embedded with `PrivateAssets="all"`). Companion packages (`FluentValidation`, `HybridCache`, `OpenTelemetry`) receive `Abstractions` transitively through `DSoftStudio.Mediator`.
- **CI workflow** — Build & Test and Publish are now separate jobs. The `publish` job uses `needs: build-and-test`, guaranteeing that **no package is published if any test fails**. GitHub Packages receives packages on every push to `main`; NuGet.org only on version tags (`v*`).
- **Companion packages bumped to 1.0.3-rc.2** — `DSoftStudio.Mediator.FluentValidation` (FluentValidation 12.1.1), `DSoftStudio.Mediator.HybridCache` (HybridCache 10.4.0), `DSoftStudio.Mediator.OpenTelemetry` (OpenTelemetry 1.15.0) updated with latest dependency versions.
- **Abstractions simplified to `netstandard2.0` only** — Removed `net8.0` multi-target; `netstandard2.0` provides maximum compatibility across all .NET versions.
- **Test infrastructure migrated to xunit v3** — All 7 test projects updated from xunit v2 to xunit v3 (3.2.2) with `xunit.runner.visualstudio` 3.1.5 and `Microsoft.NET.Test.Sdk` 18.3.0.

---

## [1.1.4] — 2026-03-19

### Fixed

- **CS0436 / CS0121 with `InternalsVisibleTo`** — Source-generated types no longer conflict when a test project (or any referencing project) has `InternalsVisibleTo` access to the host project. ([#1](https://github.com/DSoftStudio/Mediator/issues/1))
  - Generated worker/implementation classes now use the C# 11 `file` modifier, making them invisible across assemblies.
  - Generated extension methods are emitted into per-assembly unique namespaces (`DSoftStudio.Mediator.Generated.{AssemblyName}`) with a `global using` for transparent usage.
- **Cross-project handler discovery** — Handlers defined in referenced projects are now discovered automatically via `[assembly: MediatorHandlerRegistration]` attributes, replacing the previous PE metadata scanning approach that could miss `internal` members.
- **`Send(object)` namespace shadowing** — The runtime `Send(object)` extension is now generated alongside typed extensions in the per-assembly namespace, preventing C# resolution from shadowing typed overloads.

### Changed

- `Send(object)` is now fully source-generated — removed `SenderObjectExtensions.cs` from the runtime DLL.

---

## [1.1.3] — 2026-03-15

### Changed

- **Documentation site** — all doc links now point to [docs.dsoftstudio.com/mediator](https://docs.dsoftstudio.com/mediator) instead of relative GitHub paths.
- **Project website** — NuGet "Project website" updated to `https://docs.dsoftstudio.com/mediator`.

### Fixed

- **SonarCloud quality gate** — excluded `docs/`, `samples/`, and `benchmarks/` from analysis to prevent false-positive bugs on non-production code.

---

## [1.1.2] — 2026-03-15

### Fixed

- **NuGet README rendering** — replaced `<picture>` HTML tag with pure Markdown image syntax (`![alt](url)`) across all package READMEs. NuGet does not support `<picture>` or `<p align="center">` HTML tags, causing raw HTML to render on package pages.

---

## [1.1.1] — 2026-03-15

### Fixed

- **`Send(object)` dispatch fails when multiple `ServiceProvider` instances coexist** —
  The `Send(object)` runtime dispatch delegate referenced the static
  `RequestDispatch<TRequest, TResponse>.Pipeline` field, which is write-once
  (`Interlocked.CompareExchange`). When parallel test classes (or multi-tenant hosts)
  created separate `ServiceProvider` instances with different pipeline configurations,
  the first registration won the static slot. Subsequent providers that lacked
  `PipelineChainHandler` registrations threw `InvalidOperationException`.
  The delegate now resolves directly from the passed-in `IServiceProvider` via
  `GetService<PipelineChainHandler<TRequest, TResponse>>()` (nullable probe) with
  fallback to `GetRequiredService<IRequestHandler<TRequest, TResponse>>()`,
  making it independent of static initialization order.

---

## [1.1.0] — 2026-03-15

### Added

- **Self-handling requests** — request classes (or records) that implement `IRequest<T>`,
  `ICommand<T>`, or `IQuery<T>` and contain a `static Execute` method are automatically
  discovered at compile time and wired into the mediator pipeline. No separate handler
  class is required. The source generator emits an internal adapter that bridges the
  static method to `IRequestHandler<TRequest, TResponse>`, preserving the same
  zero-overhead dispatch path (`HandlerCache`, pipeline behaviors, typed extensions,
  and handler validation).

  Supported return types: `T` (sync), `Task<T>`, `ValueTask<T>`, `void` (Unit),
  `Task` (async Unit).

  DI injection: service parameters in the `Execute` signature are resolved from DI
  automatically. Stateless self-handlers (no DI services) are registered as Singleton;
  with DI dependencies as Transient.

  Full pipeline integration: behaviors, pre/post processors, exception handlers,
  typed `Send()` extensions, and `ValidateMediatorHandlers()` all work with
  self-handling requests.

- **Fail-fast handler validation** — new source-generated `ValidateMediatorHandlers()`
  extension method on `IServiceProvider`. Resolves every mediator handler from DI at
  startup and throws an `AggregateException` with all failures if any handler is
  misconfigured. Detects missing registrations, broken constructor dependencies, and
  incomplete pipeline configurations before the first request is processed.

  ```csharp
  var app = builder.Build();
  app.Services.ValidateMediatorHandlers(); // throws AggregateException if misconfigured
  ```

- **DSOFT002: Duplicate request handler** — compile-time diagnostic (Warning) when
  multiple `IRequestHandler<TRequest, TResponse>` implementations are found for the
  same `<TRequest, TResponse>` pair. With Microsoft.Extensions.DI, only the last
  registration is resolved via `GetRequiredService<T>()` — earlier handlers are
  silently ignored. The diagnostic lists all conflicting implementations.

- **DSOFT003: Duplicate stream handler** — compile-time diagnostic (Warning) when
  multiple `IStreamRequestHandler<TRequest, TResponse>` implementations are found for
  the same `<TRequest, TResponse>` pair. Same root cause as DSOFT002.

- **Runtime-typed `Send(object)` dispatch** — new `Send(this ISender, object, CancellationToken)`
  extension method for message bus / command queue scenarios where the consumer only has
  an `object` reference at runtime. Uses a compile-time generated
  `FrozenDictionary<Type, DispatchDelegate>` dispatch table (same architecture as
  `Publish(object)`) — no reflection, no `MakeGenericType`, fully AOT-safe.

  The extension method design preserves overload resolution: generated typed extensions
  (e.g. `Send(this ISender, Ping)`) are always preferred when the compile-time type is
  known. `Send(object)` is only selected when the argument is typed as `object`.

  Zero impact on the existing `Send<TRequest, TResponse>()` hot path — completely
  separate dispatch table and code path.

  See [ADR-0004](docs/mediator/adr/0004-runtime-typed-send.md) for design rationale.

- **`DSoftStudio.Mediator.OpenTelemetry` package** — New companion NuGet package providing
  automatic distributed tracing and metrics for all mediator operations via standard
  `IPipelineBehavior<,>`, `IStreamPipelineBehavior<,>`, and an `INotificationPublisher`
  decorator — zero changes to the core mediator library.

  **Tracing:** Single `ActivitySource("DSoftStudio.Mediator")` with span names following
  `{TypeName} {kind}` convention (e.g. `CreateUser command`, `GetUsers query`).
  Span attributes include `mediator.request.type`, `mediator.response.type`, and
  `mediator.request.kind` (`command`/`query`/`request`/`notification`/`stream`).
  Exception recording with configurable stack traces.

  **Metrics:** Single `Meter("DSoftStudio.Mediator")` with three instruments:
  `mediator.request.duration` (histogram, seconds), `mediator.request.active`
  (up-down counter), `mediator.request.errors` (counter with `error.type` tag).

  **Notification instrumentation:** `InstrumentedNotificationPublisher` decorator
  creates a parent span per `Publish()` call with per-handler child spans — unique
  among .NET mediator libraries.

  **Zero-cost when unused:** `HasListeners()` / `Instrument.Enabled` short-circuits
  add ~1 ns when no OTel exporter is configured.

  **Configuration:** `AddMediatorInstrumentation()` with options for filtering
  (suppress health checks), enrichment (custom tags), and independent tracing/metrics
  toggles.

  See [ADR-0005](docs/mediator/adr/0005-opentelemetry-instrumentation.md) for design rationale.

- **`DSoftStudio.Mediator.FluentValidation` package** — New companion NuGet package
  providing automatic request validation via FluentValidation. Registers a single
  open-generic `ValidationBehavior<TRequest, TResponse>` pipeline behavior that
  resolves all `IValidator<TRequest>` instances from DI, runs validation before the
  handler, and throws `MediatorValidationException` on failure.

  **Key features:**
  - Aggregates failures from multiple validators per request type
  - `MediatorValidationException.ErrorsByProperty` for easy `ValidationProblemDetails` mapping
  - Zero-overhead pass-through when no validators are registered for a request type
  - Validators support full DI (constructor injection) — no static registry
  - Single extension method: `services.AddMediatorFluentValidation()`

- **`DSoftStudio.Mediator.HybridCache` package** — New companion NuGet package
  providing automatic query/request caching via Microsoft's `HybridCache`
  (`Microsoft.Extensions.Caching.Hybrid`). Registers a single open-generic
  `CachingBehavior<TRequest, TResponse>` pipeline behavior that checks if the
  request implements `ICachedRequest` and caches results via `HybridCache.GetOrCreateAsync()`.

  **Key features:**
  - Multi-layer caching (L1 in-memory + optional L2 distributed) via `HybridCache`
  - Built-in stampede prevention — concurrent requests for the same key share one execution
  - `ICachedRequest` marker interface with `CacheKey` and `Duration` (default: 60s)
  - Zero-overhead pass-through when the request does not implement `ICachedRequest`
  - Single extension method: `services.AddMediatorHybridCache()`

### Changed

- Internal `HandlerInfo` struct in `DependencyInjectionGenerator` refactored to use
  C# primary constructor (IDE0290).

### Architecture Decisions Recorded

- **ADR-0004: Runtime-Typed Send(object) Dispatch** — Accepted. Adds `Send(object)`
  as an extension method (not interface method) using a compile-time generated
  `FrozenDictionary` dispatch table. Extension method design is required because
  `ISender.Send<TRequest, TResponse>` has two generic type parameters that cannot be
  inferred — an instance `Send(object)` would shadow all generated typed extensions
  due to C# overload resolution rules. See [`docs/mediator/adr/0004-runtime-typed-send.md`](docs/mediator/adr/0004-runtime-typed-send.md).

- **ADR-0005: OpenTelemetry Instrumentation Package** — Accepted. Separate NuGet
  package (`DSoftStudio.Mediator.OpenTelemetry`) providing automatic distributed
  tracing and metrics via standard pipeline behaviors, with zero impact on the core
  mediator library. See [`docs/mediator/adr/0005-opentelemetry-instrumentation.md`](docs/mediator/adr/0005-opentelemetry-instrumentation.md).

---

## [1.0.6] - 2026-03-12

### Fixed

- **Open-generic pipeline behavior detection** — `MediatorPipelineGenerator` now checks `IsGenericTypeDefinition` for `IPipelineBehavior<,>`, `IRequestPreProcessor<>`, `IRequestPostProcessor<,>`, and `IRequestExceptionHandler<,>`, fixing a bug where behaviors registered as open generics were silently skipped.
- **`IStreamRequestHandler<TRequest, TResponse>` covariance** — `TResponse` changed from invariant to `out` to match the `IStreamRequest<out TResponse>` contract.

### Performance

- **ThreadStatic pipeline chain caches** — `PipelineChainCache<TRequest, TResponse>` and `StreamPipelineChainCache<TRequest, TResponse>` cache Scoped/Singleton chains per-thread, eliminating a `GetService` call on the hot path. Transient chains continue resolving fresh each call.
- **Handler resolution cache** — `HandlerCache<TRequest, TResponse>` replaces `GetRequiredService` on every `Send()` with a cached resolution.
- **Pre-linked stream behavior chain** — `StreamPipelineChainHandler` now pre-links the behavior chain at construction (like `PipelineChainHandler`), removing mutable state (`_behaviorIndex`, `_active`, `Interlocked`) from the hot path.
- **`SequentialNotificationPublisher` optimized** — Materialize handlers to array once; index-based `for` loop with `IsCompletedSuccessfully` short-circuit; `AwaitRemaining` resumes from `currentIndex + 1` instead of re-scanning with `ReferenceEquals`.
- **`IsPipelineChainCacheable` / `IsStreamChainCacheable`** — New `Volatile.Read`/`Volatile.Write` static flags in `RequestDispatch<T,R>` and `StreamDispatch<T,R>` for zero-cost cache-vs-resolve branching.

### AOT & Trimming

- **Eliminate `MakeGenericType` + `Expression.Compile` from `Publish(object)`** — The `NotificationHandlerWrapper` / `NotificationHandlerWrapperImpl<T>` pattern (runtime reflection) replaced with `NotificationObjectDispatch`, a compile-time generated dispatch table. Fully AOT/trimmer-safe.
- **Delete `NotificationDispatcher`** — Replaced with `NotificationCachedDispatcher` (compile-time dispatch with handler caching).
- **Delete `NotificationHandlerWrapper` / `NotificationHandlerWrapperImpl<T>`** — No longer needed; AOT dispatch table handles all scenarios.
- **Move `IServiceProviderAccessor` from Abstractions to core** — Interceptor-internal interface no longer exposed in the public Abstractions assembly.
- **Mark Abstractions assembly as trimmable/AOT-compatible** — Added `IsTrimmable` and conditional `IsAotCompatible` to the Abstractions csproj.

### Code Quality

- **CA1068** — `CancellationToken` moved to last parameter in `PipelineChainHandler.AwaitPostProcessorAndContinue`, `SequentialNotificationPublisher.AwaitRemaining`, `NotificationCachedDispatcher`.
- **S2699** — Added assertions to `PublishTests` and `NotificationWrapperTests`.
- **CA2211** — Static field visibility fixes.
- **xUnit1031** — Replaced blocking `.Result` calls with `await` in tests.
- **Cognitive complexity** — Extracted `InterceptorHelpers` (shared `ImplementsInterface`, `ResolveRequestParameter`), refactored `ReferencedAssemblyScanner.CollectHandlersFromAssembly`, extracted `TryResolveInferredTypes` in `SendInterceptorGenerator`, extracted `PipelineChainHandler.ComputePipelineMode`.
- **`Unit` operators** — Added `<`, `>`, `<=`, `>=` comparison operators (CA1036).
- **False positive suppressions** — S2326, S2743, S3267.

### Testing

- **Performance regression tests** — `AllocationRegressionTests` and `ThroughputRegressionTests` with CI-safe thresholds (Send = 50 µs, Publish = 50 µs, Stream = 100 µs; Send = 128 B, Publish = 64 B, Stream = 512 B).

### Benchmarks

- **Added Mediator (martinothamar/Mediator) 3.0.1** to comparison suite.
- **Updated all benchmark results** — Send ~7 ns, Publish ~8.5 ns (down from ~18 ns each).
- **Updated `generate-benchmarks-md.ps1`** with isolated vs. combined run variance note.

### Documentation

- **Major README rewrite** — 4-way latency/allocation comparison tables (DSoft, Mediator SG, DispatchR, MediatR), feature comparison table, updated messaging.

### CI/CD

- **SonarCloud workflow** — `.github/workflows/sonar.yml` with Coverlet/OpenCover coverage, sample/benchmark exclusions.

### Stream Pipeline

- **Lifetime-aware stream chain registration** — `StreamGenerator` registers `StreamPipelineChainHandler` as Singleton/Scoped/Transient based on component lifetimes.
- **No-behaviors fast path for streams** — When no stream behaviors are registered, the generated pipeline resolves the handler directly, skipping chain allocation.
