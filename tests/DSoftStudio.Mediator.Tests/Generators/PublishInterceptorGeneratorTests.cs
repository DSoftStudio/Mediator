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
    public void Intercepts_Explicit_Generic_Publish_On_Release_Build()
    {
        // Explicit Publish<OrderPlaced>(...) (vs the inferred form above) on a Release build — covers the
        // explicit type-argument path and the Release emit branch.
        const string explicitPublish = """
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record OrderPlaced(int Id) : INotification;

            public static class Consumer
            {
                public static Task Run(IPublisher publisher)
                    => publisher.Publish<OrderPlaced>(new OrderPlaced(1));
            }
            """;

        var (result, output) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(
            explicitPublish, interceptors: true, release: true);

        result.GeneratedSources.ShouldNotBeEmpty();
        result.AllSource().ShouldContain("InterceptsLocation");
        output.GetDiagnostics().Where(d => d.Id == "CS9137").ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Publish_On_Non_Publisher_Type()
    {
        // Publish<T>() on a type that is NOT IPublisher must not be intercepted. Covers the receiver exclusion.
        const string nonPublisher = """
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record OrderPlaced(int Id) : INotification;

            public sealed class Faker
            {
                public Task Publish<TNotification>(TNotification n) => Task.CompletedTask;
            }

            public static class Consumer
            {
                public static Task Run(Faker f) => f.Publish(new OrderPlaced(1));
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(nonPublisher, interceptors: true);

        result.GeneratedSources
            .SelectMany(s => s.SourceText.ToString().Split('\n'))
            .Where(l => l.Contains("InterceptsLocation"))
            .ShouldBeEmpty("Publish on a non-IPublisher type must not be intercepted");
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

    [Fact]
    public void Ignores_Open_Generic_Publish_Call_Site()
    {
        // A generic forwarding method — publisher.Publish<TNotification>(notification) with an OPEN type
        // parameter — cannot be intercepted; the call dispatches through Mediator.Publish at runtime. The
        // generator must skip it. Regression: it used to emit an interceptor referencing TNotification → CS0246.
        const string openGeneric = """
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public static class Dispatcher
            {
                public static Task Publish<TNotification>(IPublisher publisher, TNotification notification)
                    where TNotification : INotification
                    => publisher.Publish(notification);
            }
            """;

        var (result, output) = GeneratorTestHarness.Run<PublishInterceptorGenerator>(openGeneric, interceptors: true);

        result.AllSource().ShouldNotContain("InterceptsLocation",
            customMessage: "an open-generic Publish call site must not be intercepted");
        output.GetDiagnostics().Where(d => d.Id == "CS0246").ShouldBeEmpty(
            "the generated interceptor must not reference unbound type parameters");
    }
}
