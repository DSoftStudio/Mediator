// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Drives the real <see cref="PublishInterceptorGenerator"/> in-memory. It intercepts
/// <c>IPublisher.Publish&lt;TNotification&gt;()</c> call sites (where the argument implements
/// <c>INotification</c>) to skip interface dispatch on the publish hot path — zero coverage before this.
/// </summary>
public class PublishInterceptorGeneratorTests
{
    private const string PublishCallSite = """
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record OrderPlaced(int Id) : INotification;

        public static class Consumer
        {
            public static Task Run(IPublisher publisher)
                => publisher.Publish(new OrderPlaced(1));
        }
        """;

    [Fact]
    public void Emits_Interceptor_For_Publish_CallSite()
    {
        var (result, output) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(
            PublishCallSite, interceptors: true);

        var code = result.AllSource();

        result.GeneratedSources.ShouldNotBeEmpty(
            "PublishInterceptorGenerator should intercept publisher.Publish(new OrderPlaced(1))");
        code.ShouldContain("InterceptsLocation");
        code.ShouldContain("DSoftStudio.Mediator.Generated");
        output.GetDiagnostics().Where(d => d.Id == "CS9137").ShouldBeEmpty();
    }

    [Fact]
    public void Does_Not_Intercept_Publish_Object_Overload()
    {
        // Publish(object) — the argument does NOT implement INotification, so the generator must skip it
        // (only the strongly-typed Publish<TNotification> hot path is intercepted). Covers the exclusion branch.
        const string objectOverload = """
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public static class Consumer
            {
                public static Task Run(IPublisher publisher)
                    => publisher.Publish((object)"not a notification");
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(objectOverload, interceptors: true);

        result.GeneratedSources
            .SelectMany(s => s.SourceText.ToString().Split('\n'))
            .Where(line => line.Contains("InterceptsLocation"))
            .ShouldBeEmpty("Publish(object) must not be intercepted");
    }
}
