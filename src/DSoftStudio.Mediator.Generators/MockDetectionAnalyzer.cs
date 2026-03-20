// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Incremental generator that detects test projects referencing a mocking library
/// alongside the DSoftStudio.Mediator source generator (with interceptors enabled).
/// <para>
/// In Release builds, interceptors use a branchless <c>castclass IServiceProviderAccessor</c>
/// pattern that throws <see cref="InvalidCastException"/> when the sender is a mock object.
/// This analyzer emits <c>DSOFT004</c> at build time so the developer can either:
/// <list type="bullet">
///   <item>Reference only <c>DSoftStudio.Mediator.Abstractions</c> in the test project, or</item>
///   <item>Set <c>&lt;DSoftMediatorSuppressInterceptors&gt;true&lt;/DSoftMediatorSuppressInterceptors&gt;</c>.</item>
/// </list>
/// </para>
/// </summary>
[Generator]
public sealed class MockDetectionAnalyzer : IIncrementalGenerator
{
    // Well-known mocking library assembly name prefixes.
    private static readonly string[] MockingAssemblyNames = new[]
    {
        "Moq",
        "NSubstitute",
        "FakeItEasy",
        "Telerik.JustMock",
        "RhinoMocks",
        "NimbleMocks"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Combine compilation (for referenced assemblies) with global analyzer options
        // (for the DSoftMediatorSuppressInterceptors MSBuild property).
        var compilationAndOptions = context.CompilationProvider
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(compilationAndOptions, static (spc, pair) =>
        {
            var (compilation, optionsProvider) = pair;

            // If interceptors are already suppressed, no warning needed.
            if (IsSuppressed(optionsProvider.GlobalOptions))
                return;

            // Look for a mocking library among the referenced assemblies.
            string detectedMockLib = DetectMockingLibrary(compilation);
            if (detectedMockLib == null)
                return;

            // Mocking library found + interceptors active → emit DSOFT004.
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MockingWithInterceptorsInRelease,
                Location.None,
                detectedMockLib));
        });
    }

    private static bool IsSuppressed(AnalyzerConfigOptions globalOptions)
    {
        return globalOptions.TryGetValue(
                   "build_property.DSoftMediatorSuppressInterceptors", out var value)
               && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectMockingLibrary(Compilation compilation)
    {
        foreach (var assembly in compilation.ReferencedAssemblyNames)
        {
            for (int i = 0; i < MockingAssemblyNames.Length; i++)
            {
                if (assembly.Name.Equals(MockingAssemblyNames[i], StringComparison.OrdinalIgnoreCase)
                    || assembly.Name.StartsWith(MockingAssemblyNames[i] + ".", StringComparison.OrdinalIgnoreCase))
                {
                    return MockingAssemblyNames[i];
                }
            }
        }

        return null;
    }
}
