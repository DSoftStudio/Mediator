// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// <c>DSOFT012</c> — a cached response type with no serializer, in a build that publishes without a
/// JIT.
/// <para>
/// HybridCache serializes every payload it caches, and <c>AddHybridCache</c> pre-registers a
/// serializer for exactly two types: <c>string</c> and <c>byte[]</c>. Everything else reaches the
/// reflection-based System.Text.Json fallback, which Native AOT and trimming disable. Measured on a
/// native binary: build succeeds, publish succeeds, application starts, and the first dispatch of a
/// cacheable request throws from inside Microsoft's serializer — naming neither the request nor the
/// remedy.
/// </para>
/// <para>
/// Two halves matter equally here. The rule has to fire where the crash would happen, and it has to
/// stay silent everywhere else: an ordinary JIT build, a response type that already has a
/// serializer, and — the one that decides whether anyone keeps the rule enabled — an application
/// that did register one.
/// </para>
/// </summary>
public class CachedResponseSerializerAnalyzerTests
{
    /// <summary>
    /// The companion is not referenced by this harness, so the abstractions it looks up by metadata
    /// name are declared here. That is also a small proof in itself: the rule couples to a NAME, not
    /// to an assembly, which is what keeps the core generator free of the companion.
    /// </summary>
    private const string Stubs = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        namespace DSoftStudio.Mediator.Abstractions
        {
            public interface IRequest<out TResponse> { }
        }

        namespace DSoftStudio.Mediator.HybridCache
        {
            public interface ICachedRequest
            {
                string CacheKey { get; }
                TimeSpan Duration { get; }
            }

            public interface IHybridCacheSerializer<T> { }
        }

        namespace Microsoft.Extensions.DependencyInjection
        {
            // Shaped like the real call, because the rule reads call sites: a bare typeof in an
            // expression body is not an invocation and would prove nothing about production code.
            public static class Reg
            {
                public static void AddKeyedSingleton<T>(object key, object value) { }
            }
        }

        namespace TestApp
        {
            using DSoftStudio.Mediator.Abstractions;
            using DSoftStudio.Mediator.HybridCache;

            public sealed record ProductDto(int Id);

        """;

    private const string CachedProductQuery = """
            public sealed record GetProduct : IRequest<ProductDto>, ICachedRequest
            {
                public string CacheKey => "p";
                public TimeSpan Duration => TimeSpan.FromMinutes(1);
            }
        """;

    // Bare property names: the harness's options double prepends nothing and STRIPS the
    // "build_property." prefix off the key the generator asks for before looking it up, so a
    // dictionary written with the prefix never matches and every positive case goes quietly green.
    private static readonly Dictionary<string, string> PublishingAot =
        new() { ["PublishAot"] = "true" };

    private static string[] Run(string body, Dictionary<string, string>? properties)
    {
        var (result, _) = GeneratorTestHarness.Run<CachedResponseSerializerAnalyzer>(
            Stubs + body + "\n}\n", buildProperties: properties);

        return result.Diagnostics
            .Where(d => d.Id == "DSOFT012")
            .Select(d => d.GetMessage())
            .ToArray();
    }

    [Fact]
    public void Warns_When_Publishing_Aot_Without_A_Serializer()
    {
        var reported = Run(CachedProductQuery, PublishingAot);

        reported.Length.ShouldBe(1);
        reported[0].ShouldContain("GetProduct", customMessage: "the request the author can act on is not named");
        reported[0].ShouldContain("ProductDto", customMessage: "the type that needs registering is not named");
    }

    [Fact]
    public void Warns_The_Same_Way_For_A_Trimmed_Build()
    {
        Run(CachedProductQuery, new Dictionary<string, string> { ["PublishTrimmed"] = "true" })
            .Length.ShouldBe(1);
    }

    /// <summary>
    /// The half that decides whether the rule is worth having on. An ordinary build keeps the
    /// reflective fallback, so there is nothing to do and nothing to say.
    /// </summary>
    [Fact]
    public void Says_Nothing_For_An_Ordinary_Build()
    {
        Run(CachedProductQuery, properties: null).ShouldBeEmpty();
        Run(CachedProductQuery, new Dictionary<string, string> { ["PublishAot"] = "false" })
            .ShouldBeEmpty();
    }

    [Fact]
    public void Says_Nothing_About_A_String_Response()
    {
        Run("""
                public sealed record GetName : IRequest<string>, ICachedRequest
                {
                    public string CacheKey => "n";
                    public TimeSpan Duration => TimeSpan.FromMinutes(1);
                }
            """, PublishingAot).ShouldBeEmpty();
    }

    [Fact]
    public void Says_Nothing_About_A_Request_That_Is_Not_Cached()
    {
        Run("""
                public sealed record PlainQuery : IRequest<ProductDto>;
            """, PublishingAot).ShouldBeEmpty();
    }

    /// <summary>
    /// The blanket registration the documentation recommends: one keyed on the OPEN generic, which
    /// covers every cached type at once.
    /// </summary>
    [Fact]
    public void Says_Nothing_When_The_Open_Generic_Key_Is_Registered()
    {
        Run(CachedProductQuery + """

                public static class Startup
                {
                    public static void Configure()
                        => global::Microsoft.Extensions.DependencyInjection.Reg
                            .AddKeyedSingleton<object>(typeof(IHybridCacheSerializer<>), new object());
                }
            """, PublishingAot).ShouldBeEmpty();
    }

    [Fact]
    public void Says_Nothing_When_That_One_Type_Is_Registered()
    {
        Run(CachedProductQuery + """

                public static class Startup
                {
                    public static void Configure()
                        => global::Microsoft.Extensions.DependencyInjection.Reg
                            .AddKeyedSingleton<object>(typeof(IHybridCacheSerializer<ProductDto>), new object());
                }
            """, PublishingAot).ShouldBeEmpty();
    }

    /// <summary>
    /// A registration for a DIFFERENT type must not silence the one that is actually missing.
    /// </summary>
    [Fact]
    public void Still_Warns_When_Only_Another_Type_Is_Registered()
    {
        Run(CachedProductQuery + """

                public sealed record OtherDto(int N);

                public static class Startup
                {
                    public static void Configure()
                        => global::Microsoft.Extensions.DependencyInjection.Reg
                            .AddKeyedSingleton<object>(typeof(IHybridCacheSerializer<OtherDto>), new object());
                }
            """, PublishingAot).Length.ShouldBe(1);
    }
}
