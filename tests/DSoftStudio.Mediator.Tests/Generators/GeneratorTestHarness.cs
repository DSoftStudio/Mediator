// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
        foreach (var facade in new[]
                 {
                     "netstandard.dll",
                     "System.Threading.Tasks.Extensions.dll",
                     "System.Collections.dll",
                     "System.Linq.dll",
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
        string source, bool interceptors = false)
        where TGenerator : IIncrementalGenerator, new()
    {
        var features = new Dictionary<string, string>();
        if (interceptors)
        {
            features["InterceptorsNamespaces"] = "DSoftStudio.Mediator.Generated";
            features["InterceptorsPreviewNamespaces"] = "DSoftStudio.Mediator.Generated";
        }

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithFeatures(features);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs")],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new IIncrementalGenerator[] { new TGenerator() }.Select(GeneratorExtensions.AsSourceGenerator),
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult().Results.Single(), output);
    }

    /// <summary>All documents this generator emitted, concatenated — for substring assertions.</summary>
    public static string AllSource(this GeneratorRunResult result)
        => string.Concat(result.GeneratedSources.Select(s => s.SourceText.ToString()));
}
