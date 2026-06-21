// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Drives the real <see cref="SendInterceptorGenerator"/> in-memory. It intercepts
/// <c>ISender.Send&lt;TRequest, TResponse&gt;()</c> call sites to skip interface dispatch on the request hot
/// path. The existing InterceptorNamespaceCompilationTests only exercise the explicit-generic Debug path; these
/// cover the inferred call, the Release path, and the exclusion branches.
/// </summary>
public class SendInterceptorGeneratorTests
{
    private const string SendCallSite = """
        using System.Threading;
        using System.Threading.Tasks;
        using DSoftStudio.Mediator.Abstractions;

        namespace TestApp;

        public record Ping(int N) : IRequest<string>;

        public sealed class PingHandler : IRequestHandler<Ping, string>
        {
            public ValueTask<string> Handle(Ping request, CancellationToken ct) => new("pong");
        }

        public static class Consumer
        {
            public static async Task<string> Run(ISender sender) => await sender.Send<Ping, string>(new Ping(1));
        }
        """;

    [Fact]
    public void Intercepts_Explicit_Send_On_Release_Build()
    {
        var (result, output) = GeneratorTestHarness.Run<SendInterceptorGenerator>(
            SendCallSite, interceptors: true, release: true);

        result.GeneratedSources.ShouldNotBeEmpty();
        result.AllSource().ShouldContain("InterceptsLocation");
        output.GetDiagnostics().Where(d => d.Id == "CS9137").ShouldBeEmpty();
    }

    [Fact]
    public void Intercepts_Type_Inferred_Send_Via_Generated_Extension()
    {
        // sender.Send(request) (no <Ping,string>) only binds once MediatorExtensionsGenerator emits the typed
        // Send(this ISender, Ping) extension; running both generators in sequence, the inferred call is intercepted.
        const string inferred = """
            using System.Threading;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping(int N) : IRequest<string>;

            public sealed class PingHandler : IRequestHandler<Ping, string>
            {
                public ValueTask<string> Handle(Ping request, CancellationToken ct) => new("pong");
            }

            public static class Consumer
            {
                public static async Task<string> Run(ISender sender) => await sender.Send(new Ping(1));
            }
            """;

        var (result, _) = GeneratorTestHarness.RunChain<MediatorExtensionsGenerator, SendInterceptorGenerator>(
            inferred, interceptors: true);

        result.AllSource().ShouldContain("InterceptsLocation");
    }

    [Fact]
    public void Ignores_Send_On_Non_Sender_Type()
    {
        const string nonSender = """
            using System.Threading.Tasks;

            namespace TestApp;

            public sealed class Faker
            {
                public Task<TResponse> Send<TRequest, TResponse>(TRequest r) => Task.FromResult(default(TResponse)!);
            }

            public static class Consumer
            {
                public static Task<string> Run(Faker f) => f.Send<int, string>(0);
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<SendInterceptorGenerator>(nonSender, interceptors: true);

        result.GeneratedSources
            .SelectMany(s => s.SourceText.ToString().Split('\n'))
            .Where(l => l.Contains("InterceptsLocation"))
            .ShouldBeEmpty("Send on a non-ISender type must not be intercepted");
    }

    [Fact]
    public void Ignores_Send_Inside_Expression_Tree_Lambda()
    {
        // A Send call captured inside an Expression<...> (e.g. a Moq Setup) must NOT be intercepted — an
        // interceptor cannot attach to an expression-tree node. Covers InterceptorHelpers.IsInsideExpressionTreeLambda.
        const string exprTree = """
            using System;
            using System.Linq.Expressions;
            using System.Threading.Tasks;
            using DSoftStudio.Mediator.Abstractions;

            namespace TestApp;

            public record Ping(int N) : IRequest<string>;

            public static class Consumer
            {
                public static Expression<Func<ISender, ValueTask<string>>> Setup()
                    => sender => sender.Send<Ping, string>(new Ping(1));
            }
            """;

        var (result, _) = GeneratorTestHarness.Run<SendInterceptorGenerator>(exprTree, interceptors: true);

        result.GeneratedSources
            .SelectMany(s => s.SourceText.ToString().Split('\n'))
            .Where(l => l.Contains("InterceptsLocation"))
            .ShouldBeEmpty("Send inside an expression-tree lambda must not be intercepted");
    }
}
