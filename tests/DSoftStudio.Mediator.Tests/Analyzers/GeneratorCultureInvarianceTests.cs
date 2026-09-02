// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using DSoftStudio.Mediator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DSoftStudio.Mediator.Tests.Analyzers;

/// <summary>
/// Generated source must not depend on the build machine's locale.
/// <para>
/// The generators order handlers by type name to make their output deterministic. Ordering with the
/// default string comparer makes it deterministic per CULTURE instead: <c>Comparer&lt;string&gt;.Default</c>
/// goes through <c>string.CompareTo</c>, which is culture-sensitive. Under <c>da-DK</c>, "Aa" collates
/// as "Å" and therefore sorts AFTER "Z", so the same source produced a different dispatch order — and
/// a different generated file — on a Danish machine than on an English one.
/// </para>
/// </summary>
public class GeneratorCultureInvarianceTests
{
    private const string AbstractionsSource = """
        namespace DSoftStudio.Mediator.Abstractions
        {
            public interface INotification { }

            public interface INotificationHandler<in TNotification>
                where TNotification : INotification
            {
                System.Threading.Tasks.Task Handle(
                    TNotification notification, System.Threading.CancellationToken ct);
            }
        }
        """;

    // "Aarhus" before "Zebra" ordinally; after it under Danish collation. Nothing else distinguishes
    // them, so the emitted order is decided purely by the comparer the generator uses.
    private const string HandlersSource = """
        using DSoftStudio.Mediator.Abstractions;

        public class Ping : INotification { }

        public class AarhusHandler : INotificationHandler<Ping>
        {
            public System.Threading.Tasks.Task Handle(Ping n, System.Threading.CancellationToken ct) => null!;
        }

        public class ZebraHandler : INotificationHandler<Ping>
        {
            public System.Threading.Tasks.Task Handle(Ping n, System.Threading.CancellationToken ct) => null!;
        }
        """;

    private static string RunUnderCulture(IIncrementalGenerator generator, string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [
                    CSharpSyntaxTree.ParseText(AbstractionsSource, path: "Abstractions.cs"),
                    CSharpSyntaxTree.ParseText(HandlersSource, path: "UserCode.cs"),
                ],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator }.Select(GeneratorExtensions.AsSourceGenerator));

            var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
            return string.Concat(result.GeneratedSources.Select(s => s.SourceText.ToString()));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NotificationGenerator_EmitsTheSameOrder_UnderDanishAndInvariantCultures()
    {
        var invariant = RunUnderCulture(new NotificationGenerator(), "en-US");
        var danish = RunUnderCulture(new NotificationGenerator(), "da-DK");

        danish.ShouldBe(invariant);
    }

    [Fact]
    public void DependencyInjectionGenerator_EmitsTheSameOrder_UnderDanishAndInvariantCultures()
    {
        var invariant = RunUnderCulture(new DependencyInjectionGenerator(), "en-US");
        var danish = RunUnderCulture(new DependencyInjectionGenerator(), "da-DK");

        danish.ShouldBe(invariant);
    }

    [Fact]
    public void DependencyInjectionGenerator_OrdersHandlersOrdinally_NotByCollation()
    {
        var danish = RunUnderCulture(new DependencyInjectionGenerator(), "da-DK");

        var aarhus = danish.IndexOf("AarhusHandler", StringComparison.Ordinal);
        var zebra = danish.IndexOf("ZebraHandler", StringComparison.Ordinal);

        aarhus.ShouldBeGreaterThanOrEqualTo(0);
        zebra.ShouldBeGreaterThanOrEqualTo(0);
        aarhus.ShouldBeLessThan(zebra);
    }
}
