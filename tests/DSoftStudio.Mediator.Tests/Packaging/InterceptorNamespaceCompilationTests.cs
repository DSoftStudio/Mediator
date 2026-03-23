// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Packaging;

/// <summary>
/// Roslyn-based compilation tests that reproduce the CS9137 bug from v1.1.6 and verify the fix.
/// <para>
/// <b>Scenario:</b> The <see cref="SendInterceptorGenerator"/> emits <c>[InterceptsLocation]</c>
/// methods in the <c>DSoftStudio.Mediator.Generated</c> namespace. If the
/// <c>InterceptorsNamespaces</c> feature flag does not include that namespace, the compiler
/// rejects the generated code with <b>CS9137</b>:
/// <em>"The 'interceptors' feature is not enabled in this namespace."</em>
/// </para>
/// <para>
/// In v1.1.6, the <c>buildTransitive/DSoftStudio.Mediator.props</c> file was missing the
/// <c>InterceptorsNamespaces</c> property, causing this error for all transitive consumers.
/// These tests validate the fix by driving the real generator in-memory.
/// </para>
/// </summary>
public class InterceptorNamespaceCompilationTests
{
    /// <summary>
    /// Minimal user source that triggers the <see cref="SendInterceptorGenerator"/>.
    /// Contains a <c>sender.Send&lt;Ping, string&gt;()</c> call site that the generator intercepts.
    /// </summary>
    private const string TestSource = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record Ping : IRequest<string>;

        public class PingHandler : IRequestHandler<Ping, string>
        {
            public ValueTask<string> Handle(Ping request, CancellationToken ct)
                => new("pong");
        }

        public static class Consumer
        {
            public static async Task<string> Run(ISender sender)
            {
                return await sender.Send<Ping, string>(new Ping());
            }
        }
        """;

    /// <summary>
    /// Creates <see cref="CSharpParseOptions"/> with or without the <c>InterceptorsNamespaces</c>
    /// feature flag — the key differentiator between the v1.1.6 bug and the v1.1.7 fix.
    /// </summary>
    private static CSharpParseOptions CreateParseOptions(bool includeInterceptorsFeature)
    {
        var features = new Dictionary<string, string>();

        if (includeInterceptorsFeature)
        {
            // Both flags required for cross-SDK compatibility (stable + preview).
            features["InterceptorsNamespaces"] = "DSoftStudio.Mediator.Generated";
            features["InterceptorsPreviewNamespaces"] = "DSoftStudio.Mediator.Generated";
        }

        return CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithFeatures(features);
    }

    /// <summary>
    /// Collects metadata references for the BCL, Abstractions, Mediator, and DI abstractions.
    /// </summary>
    private static MetadataReference[] GetMetadataReferences()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(
                typeof(DSoftStudio.Mediator.Abstractions.ISender).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(DSoftStudio.Mediator.Mediator).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions)
                    .Assembly.Location),
        };

        // Facade assemblies required for cross-TFM type unification (netstandard2.0 → .NET 10).
        string[] facades =
        [
            "netstandard.dll",
            "System.Threading.Tasks.Extensions.dll",
            "System.Collections.dll",
        ];

        foreach (var facade in facades)
        {
            var path = Path.Combine(runtimeDir, facade);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        return references.ToArray();
    }

    /// <summary>
    /// Runs the <see cref="SendInterceptorGenerator"/> against a compilation with the given
    /// parse options and returns the generator result plus the updated compilation.
    /// </summary>
    private static (GeneratorRunResult Result, Compilation OutputCompilation)
        RunSendInterceptorGenerator(CSharpParseOptions parseOptions)
    {
        var tree = CSharpSyntaxTree.ParseText(TestSource, parseOptions, path: "Test.cs");

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [tree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new SendInterceptorGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new IIncrementalGenerator[] { generator }
                .Select(GeneratorExtensions.AsSourceGenerator),
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out _);

        var result = driver.GetRunResult().Results.Single();
        return (result, outputCompilation);
    }

    // ── Test 1: Generator emits interceptor code ─────────────────────

    /// <summary>
    /// Verifies that <see cref="SendInterceptorGenerator"/> produces interceptor methods
    /// with <c>[InterceptsLocation]</c> attributes for the <c>sender.Send&lt;Ping, string&gt;()</c>
    /// call site in the test source.
    /// </summary>
    [Fact]
    public void SendInterceptorGenerator_Emits_InterceptorCode()
    {
        var parseOptions = CreateParseOptions(includeInterceptorsFeature: true);
        var (result, _) = RunSendInterceptorGenerator(parseOptions);

        result.GeneratedSources.ShouldNotBeEmpty(
            "SendInterceptorGenerator should produce interceptor code " +
            "for the sender.Send<Ping, string>() call site");

        var generatedCode = result.GeneratedSources
            .Select(s => s.SourceText.ToString())
            .Aggregate(string.Empty, (a, b) => a + b);

        generatedCode.ShouldContain("InterceptsLocation");
        generatedCode.ShouldContain("DSoftStudio.Mediator.Generated");
    }

    // ── Test 2: CS9137 WITHOUT InterceptorsNamespaces (v1.1.6 bug) ───

    /// <summary>
    /// Reproduces the v1.1.6 regression: when the <c>InterceptorsNamespaces</c> feature flag
    /// is missing, the compiler emits <b>CS9137</b> for every <c>[InterceptsLocation]</c>
    /// method — even though the generator produced valid interceptor code.
    /// </summary>
    [Fact]
    public void Interceptor_Without_InterceptorsNamespaces_Produces_CS9137()
    {
        // Simulate v1.1.6 bug: no InterceptorsNamespaces feature.
        var parseOptions = CreateParseOptions(includeInterceptorsFeature: false);
        var (result, outputCompilation) = RunSendInterceptorGenerator(parseOptions);

        // Generator should still emit interceptor code regardless of the feature flag.
        result.GeneratedSources.ShouldNotBeEmpty();

        var diagnostics = outputCompilation.GetDiagnostics();

        diagnostics.Where(d => d.Id == "CS9137").ShouldNotBeEmpty(
            "Without InterceptorsNamespaces, the compiler should reject [InterceptsLocation] " +
            "with CS9137 — this is the v1.1.6 regression");
    }

    // ── Test 3: No CS9137 WITH InterceptorsNamespaces (v1.1.7 fix) ───

    /// <summary>
    /// Validates the v1.1.7 fix: when <c>InterceptorsNamespaces</c> includes
    /// <c>DSoftStudio.Mediator.Generated</c>, the compiler accepts the interceptor methods
    /// and CS9137 does not appear.
    /// </summary>
    [Fact]
    public void Interceptor_With_InterceptorsNamespaces_No_CS9137()
    {
        // v1.1.7 fix: InterceptorsNamespaces set correctly.
        var parseOptions = CreateParseOptions(includeInterceptorsFeature: true);
        var (result, outputCompilation) = RunSendInterceptorGenerator(parseOptions);

        result.GeneratedSources.ShouldNotBeEmpty();

        var diagnostics = outputCompilation.GetDiagnostics();

        diagnostics.Where(d => d.Id == "CS9137").ShouldBeEmpty(
            "With InterceptorsNamespaces set correctly, CS9137 should not appear — " +
            "this validates the v1.1.7 fix for the buildTransitive props regression");
    }
}
