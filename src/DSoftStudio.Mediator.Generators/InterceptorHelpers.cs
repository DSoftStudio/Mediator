// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Shared helpers for interceptor generators and typed extension generators.
/// Pure static methods — no allocations, no state.
/// </summary>
internal static class InterceptorHelpers
{
    // ── Shared dispatch body builders ────────────────────────────────

    /// <summary>
    /// Appends the inline <c>Send</c> dispatch body (argument validation, service provider
    /// access, pipeline chain resolution, handler cache fallback) to <paramref name="sb"/>.
    /// <para>
    /// Used by both <see cref="SendInterceptorGenerator"/> (interceptor methods) and
    /// <see cref="MediatorExtensionsGenerator"/> (typed extension methods) so the dispatch
    /// logic is defined in a single place.
    /// </para>
    /// </summary>
    /// <param name="sb">Target builder.</param>
    /// <param name="requestType">Fully-qualified request type name.</param>
    /// <param name="responseType">Fully-qualified response type name.</param>
    /// <param name="isRelease"><see langword="true"/> for <b>interceptors</b> in Release builds
    /// (branchless castclass — test projects suppress interceptors via
    /// <c>DSoftMediatorSuppressInterceptors</c>); <see langword="false"/> for <b>typed extensions</b>
    /// and Debug interceptors (isinst with graceful virtual-dispatch fallback, ~1–2 CPU cycles
    /// overhead, ensuring consistent behaviour across Debug/Release including CI
    /// <c>dotnet test -c Release</c> pipelines).</param>
    /// <param name="indent">Whitespace prefix for each emitted line.</param>
    public static void AppendSendDispatchBody(
        StringBuilder sb,
        string requestType,
        string responseType,
        bool isRelease,
        string indent)
    {
        var i2 = indent + "    ";
        var i3 = indent + "        ";

        sb.Append(indent).AppendLine("global::System.ArgumentNullException.ThrowIfNull(request);");

        if (isRelease)
        {
            // Interceptor Release path: branchless castclass — GDV devirtualizes to ~0 ns.
            // Safe because test projects suppress interceptors via DSoftMediatorSuppressInterceptors.
            sb.Append(indent).AppendLine("var sp = ((global::DSoftStudio.Mediator.IServiceProviderAccessor)sender).ServiceProvider;");
        }
        else
        {
            // Defensive dispatch: isinst + virtual-dispatch fallback for mock/test-double safety.
            // Used by typed extensions (always) and interceptors (Debug only). ~1–2 cycle overhead.
            sb.Append(indent).AppendLine("if (sender is not global::DSoftStudio.Mediator.IServiceProviderAccessor __spa)");
            sb.Append(i2).Append("return sender.Send<")
              .Append(requestType).Append(", ").Append(responseType)
              .AppendLine(">(request, cancellationToken);");
            sb.Append(indent).AppendLine("var sp = __spa.ServiceProvider;");
        }

        // Zero-delegate dispatch: static bool skips the GetService probe for the
        // no-behaviors path (~0 ns branch vs ~5 ns failed DI lookup).
        sb.Append(indent).Append("if (global::DSoftStudio.Mediator.RequestDispatch<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.HasPipelineChain)");
        sb.Append(indent).AppendLine("{");

        // ThreadStatic cache for Scoped/Singleton chains; direct GetService for Transient.
        sb.Append(i2).Append("var chain = global::DSoftStudio.Mediator.RequestDispatch<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.IsPipelineChainCacheable");
        sb.Append(i3).Append("? global::DSoftStudio.Mediator.PipelineChainCache<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.Resolve(sp)");
        sb.Append(i3).Append(": global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions")
          .Append(".GetService<global::DSoftStudio.Mediator.PipelineChainHandler<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">>(sp);");

        sb.Append(i2).AppendLine("if (chain is not null)");
        sb.Append(i3).AppendLine("return chain.Handle(request, cancellationToken);");
        sb.Append(indent).AppendLine("}");

        sb.Append(indent).Append("return global::DSoftStudio.Mediator.HandlerCache<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.Resolve(sp).Handle(request, cancellationToken);");
    }

    /// <summary>
    /// Appends the inline <c>CreateStream</c> dispatch body (argument validation, service
    /// provider access, stream pipeline chain resolution, handler cache fallback) to
    /// <paramref name="sb"/>.
    /// <para>
    /// Used by both <see cref="StreamInterceptorGenerator"/> (interceptor methods) and
    /// <see cref="MediatorExtensionsGenerator"/> (typed extension methods).
    /// </para>
    /// </summary>
    public static void AppendStreamDispatchBody(
        StringBuilder sb,
        string requestType,
        string responseType,
        bool isRelease,
        string indent)
    {
        var i2 = indent + "    ";

        sb.Append(indent).AppendLine("global::System.ArgumentNullException.ThrowIfNull(request);");

        if (isRelease)
        {
            // Interceptor Release path: branchless castclass — GDV devirtualizes to ~0 ns.
            // Safe because test projects suppress interceptors via DSoftMediatorSuppressInterceptors.
            sb.Append(indent).AppendLine("var sp = ((global::DSoftStudio.Mediator.IServiceProviderAccessor)mediator).ServiceProvider;");
        }
        else
        {
            // Defensive dispatch: isinst + virtual-dispatch fallback for mock/test-double safety.
            // Used by typed extensions (always) and interceptors (Debug only). ~1–2 cycle overhead.
            sb.Append(indent).AppendLine("if (mediator is not global::DSoftStudio.Mediator.IServiceProviderAccessor __spa)");
            sb.Append(i2).Append("return mediator.CreateStream<")
              .Append(requestType).Append(", ").Append(responseType)
              .AppendLine(">(request, cancellationToken);");
            sb.Append(indent).AppendLine("var sp = __spa.ServiceProvider;");
        }

        // Behaviors path: check for stream pipeline chain (cached or direct DI)
        sb.Append(indent).Append("var chain = global::DSoftStudio.Mediator.StreamDispatch<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.IsStreamChainCacheable");
        sb.Append(i2).Append("? global::DSoftStudio.Mediator.StreamPipelineChainCache<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.Resolve(sp)");
        sb.Append(i2).Append(": global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions")
          .Append(".GetService<global::DSoftStudio.Mediator.StreamPipelineChainHandler<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">>(sp);");

        sb.Append(indent).AppendLine("if (chain is not null)");
        sb.Append(i2).AppendLine("return chain.Handle(request, cancellationToken);");

        // No-behaviors fast path: resolve stream handler directly via ThreadStatic cache.
        // Null guard on Handler factory matches the InvalidOperationException contract.
        sb.Append(indent).Append("var __shFactory = global::DSoftStudio.Mediator.StreamDispatch<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.Handler");
        sb.Append(i2).Append("?? throw new global::System.InvalidOperationException(\"Stream handler for \" + typeof(")
          .Append(requestType)
          .AppendLine(").Name + \" not registered.\");");
        sb.Append(indent).Append("return global::DSoftStudio.Mediator.StreamHandlerCache<")
          .Append(requestType).Append(", ").Append(responseType)
          .AppendLine(">.Resolve(sp).Handle(request, cancellationToken);");
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="containingType"/> is or implements
    /// the interface identified by <paramref name="interfaceMetadataName"/>.
    /// </summary>
    public static bool ImplementsInterface(
        INamedTypeSymbol containingType,
        Compilation compilation,
        string interfaceMetadataName)
    {
        var target = compilation.GetTypeByMetadataName(interfaceMetadataName);
        if (target is null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(containingType, target))
            return true;

        return containingType.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, target));
    }

    /// <summary>
    /// Resolves the first meaningful parameter from a method call that may be either
    /// an explicit generic call or a type-inferred extension method call.
    /// Returns <see langword="null"/> when the parameter cannot be determined.
    /// </summary>
    public static IParameterSymbol? ResolveRequestParameter(IMethodSymbol method)
    {
        if (method.IsExtensionMethod && method.ReducedFrom is not null)
            return method.Parameters[0];

        return method.Parameters.Length >= 2 ? method.Parameters[1] : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="node"/> is located inside a lambda
    /// expression whose converted type is <see cref="System.Linq.Expressions.Expression{TDelegate}"/>.
    /// <para>
    /// Interceptors must NOT rewrite call sites inside expression trees because the
    /// rewritten static extension method invocation is incompatible with expression
    /// tree compilation. This also prevents breaking mocking frameworks (Moq, NSubstitute,
    /// FakeItEasy) that inspect the expression tree passed to Setup/Verify/Received.
    /// </para>
    /// </summary>
    public static bool IsInsideExpressionTreeLambda(
        SemanticModel semanticModel,
        SyntaxNode node,
        CancellationToken ct)
    {
        var expressionOfT = semanticModel.Compilation
            .GetTypeByMetadataName("System.Linq.Expressions.Expression`1");

        if (expressionOfT is null)
            return false;

        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (current is LambdaExpressionSyntax lambda)
            {
                var typeInfo = semanticModel.GetTypeInfo(lambda, ct);
                if (typeInfo.ConvertedType is INamedTypeSymbol convertedType
                    && SymbolEqualityComparer.Default.Equals(
                        convertedType.OriginalDefinition, expressionOfT))
                {
                    return true;
                }
            }

            current = current.Parent;
        }

        return false;
    }
}
