// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Shared in-memory harness for driving the real incremental source generators against a small user
/// compilation and inspecting the generated source — exactly as the compiler runs them. Mirrors the
/// reference set proven by <c>InterceptorNamespaceCompilationTests</c> (BCL + Abstractions + Mediator +
/// DI + the cross-TFM facades needed to unify netstandard2.0 generator types with .NET 10 BCL types).
/// </summary>
internal static class GeneratorTestHarness
{
    private static readonly MetadataReference[] References = BuildReferences();

    private static MetadataReference[] BuildReferences()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(DSoftStudio.Mediator.Abstractions.ISender).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DSoftStudio.Mediator.Mediator).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions).Assembly.Location),
        };

        // Microsoft.Bcl.AsyncInterfaces — the netstandard2.0 Abstractions declares IAsyncEnumerable<T> (used by
        // every stream API: IStreamRequestHandler.Handle, IMediator.CreateStream) from this package. Without it,
        // stream call sites/handlers fail to bind (CS0012) and the stream generators see nothing.
        var bclAsync = Path.Combine(AppContext.BaseDirectory, "Microsoft.Bcl.AsyncInterfaces.dll");
        if (File.Exists(bclAsync))
            refs.Add(MetadataReference.CreateFromFile(bclAsync));

        // Facade assemblies required for cross-TFM type unification (netstandard2.0 → .NET 10).
        // System.ComponentModel: System.IServiceProvider is type-forwarded there on .NET 10 —
        // needed since the ADR-0065 concrete caches name the type explicitly in generated code
        // (real consumer builds always have it via the default reference pack).
        foreach (var facade in new[]
                 {
                     "netstandard.dll",
                     "System.Threading.Tasks.Extensions.dll",
                     "System.Collections.dll",
                     "System.Linq.dll",
                     "System.ComponentModel.dll",
                 })
        {
            var path = Path.Combine(runtimeDir, facade);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        return refs.ToArray();
    }

    /// <summary>
    /// Runs <typeparamref name="TGenerator"/> over <paramref name="source"/> and returns the single
    /// generator run result plus the post-generation compilation. Set <paramref name="interceptors"/> to
    /// <c>true</c> for generators that emit <c>[InterceptsLocation]</c> — they need the
    /// <c>InterceptorsNamespaces</c> feature flag or the compiler rejects the generated code with CS9137.
    /// </summary>
    public static (GeneratorRunResult Result, Compilation Output) Run<TGenerator>(
        string source, bool interceptors = false, bool release = false,
        Dictionary<string, string>? buildProperties = null)
        where TGenerator : IIncrementalGenerator, new()
    {
        var (parse, compilation) = Build(source, interceptors, release);
        var driver = DriverFor(new TGenerator(), parse, buildProperties)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult().Results.Single(), output);
    }

    /// <summary>
    /// Runs <typeparamref name="TFirst"/> then <typeparamref name="TSecond"/> in sequence, with the second
    /// generator seeing the first's emitted source. This mirrors a real build where one generator's output is a
    /// prerequisite for another — e.g. <c>MediatorExtensionsGenerator</c> emits the typed
    /// <c>CreateStream(this IMediator, T)</c> / <c>Send(this ISender, T)</c> extension that makes a
    /// type-inferred call bind, which the interceptor generator then intercepts.
    /// </summary>
    public static (GeneratorRunResult Result, Compilation Output) RunChain<TFirst, TSecond>(
        string source, bool interceptors = false, bool release = false)
        where TFirst : IIncrementalGenerator, new()
        where TSecond : IIncrementalGenerator, new()
    {
        var (parse, compilation) = Build(source, interceptors, release);
        DriverFor(new TFirst(), parse).RunGeneratorsAndUpdateCompilation(compilation, out var afterFirst, out _);
        var driver = DriverFor(new TSecond(), parse)
            .RunGeneratorsAndUpdateCompilation(afterFirst, out var output, out _);
        return (driver.GetRunResult().Results.Single(), output);
    }

    private static (CSharpParseOptions Parse, CSharpCompilation Compilation) Build(
        string source, bool interceptors, bool release)
    {
        var features = new Dictionary<string, string>();
        if (interceptors)
        {
            features["InterceptorsNamespaces"] = "DSoftStudio.Mediator.Generated";
            features["InterceptorsPreviewNamespaces"] = "DSoftStudio.Mediator.Generated";
        }

        var parse = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithFeatures(features);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source, parse, path: "Test.cs")],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                // Release flips OptimizationLevel — the interceptor generators emit slightly different code
                // (e.g. [MethodImpl(AggressiveInlining)]) on the Release path, exercised by passing release: true.
                optimizationLevel: release ? OptimizationLevel.Release : OptimizationLevel.Debug));

        return (parse, compilation);
    }

    private static GeneratorDriver DriverFor(
        IIncrementalGenerator generator,
        CSharpParseOptions parse,
        Dictionary<string, string>? buildProperties = null)
        => CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: parse,
            optionsProvider: buildProperties is null
                ? null
                : new TestAnalyzerConfigOptionsProvider(buildProperties));

    /// <summary>
    /// Surfaces MSBuild properties to the generators exactly as the real build does
    /// (<c>CompilerVisibleProperty</c> → <c>build_property.&lt;Name&gt;</c> in GlobalOptions) so
    /// tests can exercise knobs like <c>DSoftMediatorDisableAggressive</c>.
    /// </summary>
    private sealed class TestAnalyzerConfigOptionsProvider(Dictionary<string, string> properties)
        : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions _global = new(properties);

        public override AnalyzerConfigOptions GlobalOptions => _global;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;

        private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            // The real compiler's contract is case-INsensitive (AnalyzerConfigOptions.KeyComparer
            // is OrdinalIgnoreCase; editorconfig property keys compare case-insensitively), so
            // the double must be too — a casing mismatch between a test's dictionary and a
            // generator's lookup literal would otherwise false-green the opposite of production.
            private readonly Dictionary<string, string> _properties;

            public TestAnalyzerConfigOptions(Dictionary<string, string> properties)
                => _properties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);

            public override bool TryGetValue(string key, out string value)
            {
                const string prefix = "build_property.";
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && _properties.TryGetValue(key.Substring(prefix.Length), out var v))
                {
                    value = v;
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }
    }

    /// <summary>All documents this generator emitted, concatenated — for substring assertions.</summary>
    public static string AllSource(this GeneratorRunResult result)
        => string.Concat(result.GeneratedSources.Select(s => s.SourceText.ToString()));
}
