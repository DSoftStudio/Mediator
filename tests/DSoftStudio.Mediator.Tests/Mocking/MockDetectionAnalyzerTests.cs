// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DSoftStudio.Mediator.Tests.Mocking;

/// <summary>
/// Verifies that <see cref="MockDetectionAnalyzer"/> emits DSOFT004 when a mocking library
/// is referenced alongside the source generator and interceptors are not suppressed.
/// </summary>
public class MockDetectionAnalyzerTests
{
    /// <summary>
    /// Creates a <see cref="CSharpCompilation"/> with additional assembly identity references
    /// (simulating e.g. Moq being present) and optional global analyzer config entries.
    /// </summary>
    private static GeneratorDriver CreateDriverAndRun(
        string[] extraAssemblyNames,
        Dictionary<string, string>? globalOptions = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("class Placeholder { }");

        // Start with the core runtime references.
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        // Add fake assembly identity references for mocking libraries.
        foreach (var name in extraAssemblyNames)
        {
            // Build a minimal in-memory assembly with the given name.
            var stubCompilation = CSharpCompilation.Create(
                name,
                new[] { CSharpSyntaxTree.ParseText("") },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new System.IO.MemoryStream();
            var emitResult = stubCompilation.Emit(ms);
            if (!emitResult.Success)
                throw new InvalidOperationException($"Failed to emit stub assembly '{name}'.");

            ms.Position = 0;
            references.Add(MetadataReference.CreateFromImage(ms.ToArray()));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MockDetectionAnalyzer();

        var optionsProvider = globalOptions is { Count: > 0 }
            ? new TestAnalyzerConfigOptionsProvider(globalOptions)
            : null;

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new IIncrementalGenerator[] { generator }.Select(GeneratorExtensions.AsSourceGenerator),
            optionsProvider: optionsProvider);

        return driver.RunGenerators(compilation);
    }

    [Fact]
    public void Emits_DSOFT004_When_Moq_Is_Referenced()
    {
        var result = CreateDriverAndRun(new[] { "Moq" });
        var diagnostics = result.GetRunResult().Diagnostics;

        diagnostics.ShouldContain(d => d.Id == "DSOFT004");
        diagnostics.First(d => d.Id == "DSOFT004")
            .GetMessage()
            .ShouldContain("Moq");
    }

    [Fact]
    public void Emits_DSOFT004_When_NSubstitute_Is_Referenced()
    {
        var result = CreateDriverAndRun(new[] { "NSubstitute" });
        var diagnostics = result.GetRunResult().Diagnostics;

        diagnostics.ShouldContain(d => d.Id == "DSOFT004");
        diagnostics.First(d => d.Id == "DSOFT004")
            .GetMessage()
            .ShouldContain("NSubstitute");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT004_When_No_MockLibrary_Referenced()
    {
        var result = CreateDriverAndRun(Array.Empty<string>());
        var diagnostics = result.GetRunResult().Diagnostics;

        diagnostics.ShouldNotContain(d => d.Id == "DSOFT004");
    }

    [Fact]
    public void Does_Not_Emit_DSOFT004_When_Suppressed()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.DSoftMediatorSuppressInterceptors"] = "true"
        };

        var result = CreateDriverAndRun(new[] { "Moq" }, options);
        var diagnostics = result.GetRunResult().Diagnostics;

        diagnostics.ShouldNotContain(d => d.Id == "DSOFT004");
    }

    [Fact]
    public void Emits_DSOFT004_For_Moq_DotPrefixed_Assembly()
    {
        // Libraries like Moq.AutoMock should also trigger the diagnostic.
        var result = CreateDriverAndRun(new[] { "Moq.AutoMock" });
        var diagnostics = result.GetRunResult().Diagnostics;

        diagnostics.ShouldContain(d => d.Id == "DSOFT004");
    }

    [Fact]
    public void Emits_DSOFT004_When_Suppress_Property_Not_Visible_To_Generator()
    {
        // Simulates the transitive-project scenario: the user sets
        // DSoftMediatorSuppressInterceptors=true in their csproj, but the
        // CompilerVisibleProperty is missing (no buildTransitive props imported).
        // Without CompilerVisibleProperty, the generator cannot read the property
        // and DSOFT004 fires — this is the bug that buildTransitive/ fixes.
        var result = CreateDriverAndRun(new[] { "Moq" }, globalOptions: null);
        var diagnostics = result.GetRunResult().Diagnostics;

        diagnostics.ShouldContain(d => d.Id == "DSOFT004");
    }

    /// <summary>
    /// Minimal <see cref="AnalyzerConfigOptionsProvider"/> implementation for tests
    /// that need to set global (MSBuild) properties visible to the generator.
    /// </summary>
    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestGlobalOptions _global;

        public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalOptions)
            => _global = new TestGlobalOptions(globalOptions);

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions.Instance;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyOptions.Instance;

        private sealed class TestGlobalOptions : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _values;
            public TestGlobalOptions(Dictionary<string, string> values) => _values = values;

            public override bool TryGetValue(string key, out string value)
                => _values.TryGetValue(key, out value!);
        }

        private sealed class EmptyOptions : AnalyzerConfigOptions
        {
            public static readonly EmptyOptions Instance = new();
            public override bool TryGetValue(string key, out string value) { value = null!; return false; }
        }
    }
}
