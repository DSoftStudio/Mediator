// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace DSoftStudio.Mediator.Tests.Packaging;

/// <summary>
/// Regression tests for the CS9137 bug introduced in v1.1.6.
/// <para>
/// <b>Root cause:</b> When NuGet sees both <c>build/</c> and <c>buildTransitive/</c> folders
/// with the same filename, <c>buildTransitive/</c> takes priority — even for direct
/// <c>PackageReference</c> consumers. If <c>buildTransitive/DSoftStudio.Mediator.props</c>
/// is missing the <c>InterceptorsNamespaces</c> property, the source generator emits
/// interceptor methods but the compiler rejects them with <b>CS9137</b>:
/// <em>"A method cannot be used as an interceptor unless its containing namespace is listed
/// in the InterceptorsNamespaces property."</em>
/// </para>
/// <para>
/// These tests ensure both props files stay in sync and contain the required properties.
/// </para>
/// </summary>
public class BuildTransitivePropsTests
{
    private static readonly string s_solutionRoot = FindSolutionRoot();
    private static readonly string s_buildPropsPath =
        Path.Combine(s_solutionRoot, "src", "DSoftStudio.Mediator", "build", "DSoftStudio.Mediator.props");
    private static readonly string s_buildTransitivePropsPath =
        Path.Combine(s_solutionRoot, "src", "DSoftStudio.Mediator", "buildTransitive", "DSoftStudio.Mediator.props");

    // ── InterceptorsNamespaces ───────────────────────────────────────

    [Fact]
    public void Build_Props_Contains_InterceptorsNamespaces()
    {
        var doc = LoadProps(s_buildPropsPath);

        doc.Descendants("InterceptorsNamespaces")
           .ShouldNotBeEmpty("build/DSoftStudio.Mediator.props must define InterceptorsNamespaces");
    }

    [Fact]
    public void BuildTransitive_Props_Contains_InterceptorsNamespaces()
    {
        var doc = LoadProps(s_buildTransitivePropsPath);

        doc.Descendants("InterceptorsNamespaces")
           .ShouldNotBeEmpty(
               "buildTransitive/DSoftStudio.Mediator.props must define InterceptorsNamespaces — " +
               "missing this property causes CS9137 (the v1.1.6 regression)");
    }

    // ── InterceptorsPreviewNamespaces ────────────────────────────────

    [Fact]
    public void Build_Props_Contains_InterceptorsPreviewNamespaces()
    {
        var doc = LoadProps(s_buildPropsPath);

        doc.Descendants("InterceptorsPreviewNamespaces")
           .ShouldNotBeEmpty("build/DSoftStudio.Mediator.props must define InterceptorsPreviewNamespaces");
    }

    [Fact]
    public void BuildTransitive_Props_Contains_InterceptorsPreviewNamespaces()
    {
        var doc = LoadProps(s_buildTransitivePropsPath);

        doc.Descendants("InterceptorsPreviewNamespaces")
           .ShouldNotBeEmpty(
               "buildTransitive/DSoftStudio.Mediator.props must define InterceptorsPreviewNamespaces — " +
               "required for SDK versions that still use the Preview feature flag");
    }

    // ── CompilerVisibleProperty ──────────────────────────────────────

    [Fact]
    public void Build_Props_Contains_CompilerVisibleProperty()
    {
        var doc = LoadProps(s_buildPropsPath);

        doc.Descendants("CompilerVisibleProperty")
           .Where(e => (string?)e.Attribute("Include") == "DSoftMediatorSuppressInterceptors")
           .ShouldNotBeEmpty("build/ props must expose DSoftMediatorSuppressInterceptors to the generator");
    }

    [Fact]
    public void BuildTransitive_Props_Contains_CompilerVisibleProperty()
    {
        var doc = LoadProps(s_buildTransitivePropsPath);

        doc.Descendants("CompilerVisibleProperty")
           .Where(e => (string?)e.Attribute("Include") == "DSoftMediatorSuppressInterceptors")
           .ShouldNotBeEmpty(
               "buildTransitive/ props must expose DSoftMediatorSuppressInterceptors to the generator — " +
               "without this, the suppress flag is invisible in transitive-reference projects");
    }

    // ── Namespace value validation ───────────────────────────────────

    [Fact]
    public void Both_Props_Reference_Same_InterceptorNamespace()
    {
        var buildDoc = LoadProps(s_buildPropsPath);
        var transitiveDoc = LoadProps(s_buildTransitivePropsPath);

        var buildValue = buildDoc.Descendants("InterceptorsNamespaces").First().Value;
        var transitiveValue = transitiveDoc.Descendants("InterceptorsNamespaces").First().Value;

        transitiveValue.ShouldBe(buildValue,
            "buildTransitive/ InterceptorsNamespaces must match build/ — divergence causes CS9137");
    }

    [Fact]
    public void InterceptorsNamespaces_Contains_GeneratedNamespace()
    {
        var doc = LoadProps(s_buildTransitivePropsPath);
        var value = doc.Descendants("InterceptorsNamespaces").First().Value;

        value.ShouldContain("DSoftStudio.Mediator.Generated");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static XDocument LoadProps(string path)
    {
        File.Exists(path).ShouldBeTrue($"Props file not found: {path}");
        return XDocument.Load(path);
    }

    private static string FindSolutionRoot([CallerFilePath] string callerPath = "")
    {
        var dir = Path.GetDirectoryName(callerPath);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "DSoftStudio.Mediator.slnx")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not locate solution root (DSoftStudio.Mediator.slnx) walking up from: " + callerPath);
    }
}
