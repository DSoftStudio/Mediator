// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#pragma warning disable RSEXPERIMENTAL002 // GetInterceptableLocation is experimental

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// Incremental generator that intercepts <c>IMediator.CreateStream&lt;TRequest, TResponse&gt;()</c>
/// call sites and replaces them with direct pipeline invocation — eliminating virtual dispatch,
/// the <c>Mediator.CreateStream</c> method frame, and the delegate indirection on the hot path.
/// Mirrors the <see cref="SendInterceptorGenerator"/> pattern for streams.
/// </summary>
[Generator]
public sealed class StreamInterceptorGenerator : IIncrementalGenerator
{
    private const string MediatorInterfaceMetadataName =
        "DSoftStudio.Mediator.Abstractions.IMediator";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var callSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCreateStreamCandidate(node),
                transform: static (ctx, ct) => GetInterceptInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var collected = callSites.Collect();

        context.RegisterSourceOutput(collected, static (spc, calls) =>
        {
            if (calls.IsDefaultOrEmpty)
                return;

            var unique = calls.Distinct().ToList();
            var code = GenerateInterceptors(unique);

            spc.AddSource(
                "StreamInterceptors.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    /// <summary>
    /// Lightweight syntactic check: is this an invocation of .CreateStream?
    /// Matches both explicit generic (.CreateStream&lt;T,R&gt;) and type-inferred (.CreateStream) call sites.
    /// </summary>
    private static bool IsCreateStreamCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess })
            return false;

        return memberAccess.Name switch
        {
            GenericNameSyntax { Identifier.Text: "CreateStream", TypeArgumentList.Arguments.Count: 2 } => true,
            IdentifierNameSyntax { Identifier.Text: "CreateStream" } => true,
            _ => false
        };
    }

    /// <summary>
    /// Semantic check: verify the call resolves to IMediator.CreateStream and extract type info + location.
    /// </summary>
    private static InterceptCallInfo? GetInterceptInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method)
            return null;

        if (method.Name != "CreateStream")
            return null;

        string requestType;
        string responseType;

        if (method.TypeArguments.Length == 2)
        {
            // Explicit generic: mediator.CreateStream<PingStream, int>(request)
            requestType = method.TypeArguments[0]
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            responseType = method.TypeArguments[1]
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        else if (method.TypeArguments.Length == 0 && method.Parameters.Length >= 1)
        {
            if (!TryResolveInferredTypes(method, out requestType, out responseType))
                return null;
        }
        else
        {
            return null;
        }

        if (!InterceptorHelpers.ImplementsInterface(method.ContainingType, ctx.SemanticModel.Compilation, MediatorInterfaceMetadataName))
            return null;

        var interceptableLocation = ctx.SemanticModel.GetInterceptableLocation(invocation, ct);
        if (interceptableLocation is null)
            return null;

        var attributeSyntax = interceptableLocation.GetInterceptsLocationAttributeSyntax();

        return new InterceptCallInfo(
            attributeSyntax: attributeSyntax,
            requestType: requestType,
            responseType: responseType);
    }

    private static bool TryResolveInferredTypes(
        IMethodSymbol method,
        out string requestType,
        out string responseType)
    {
        requestType = responseType = string.Empty;

        var requestParam = InterceptorHelpers.ResolveRequestParameter(method);
        if (requestParam is null)
            return false;

        requestType = requestParam.Type
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Return type is IAsyncEnumerable<TResponse> — extract TResponse
        if (method.ReturnType is not INamedTypeSymbol { TypeArguments.Length: 1 } returnType)
            return false;

        responseType = returnType.TypeArguments[0]
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return true;
    }

    private static string GenerateInterceptors(List<InterceptCallInfo> calls)
    {
        var sb = new StringBuilder(2048);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS9113 // Parameter is unread (required by compiler for interceptor attribute)");
        sb.AppendLine();

        // File-local InterceptsLocation attribute — version 1 opaque format
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("namespace DSoftStudio.Mediator.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    file static class StreamInterceptors");
        sb.AppendLine("    {");

        var groups = calls
            .GroupBy(c => (c.RequestType, c.ResponseType))
            .ToList();

        int methodIndex = 0;
        foreach (var group in groups)
        {
            var reqType = group.Key.RequestType;
            var resType = group.Key.ResponseType;

            // Emit [InterceptsLocation] for each call site
            foreach (var call in group)
            {
                sb.Append("        ");
                sb.AppendLine(call.AttributeSyntax);
            }

            // Extension method on IMediator that replaces CreateStream call sites
            sb.Append("        internal static global::System.Collections.Generic.IAsyncEnumerable<");
            sb.Append(resType);
            sb.Append("> CreateStream_");
            sb.Append(methodIndex);
            sb.Append("(this global::DSoftStudio.Mediator.Abstractions.IMediator mediator, ");
            sb.Append(reqType);
            sb.AppendLine(" request, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(request);");
            sb.AppendLine("            var sp = ((global::DSoftStudio.Mediator.IServiceProviderAccessor)mediator).ServiceProvider;");

            // Behaviors path: check for stream pipeline chain (cached or direct DI)
            sb.Append("            var chain = global::DSoftStudio.Mediator.StreamDispatch<");
            sb.Append(reqType);
            sb.Append(", ");
            sb.Append(resType);
            sb.AppendLine(">.IsStreamChainCacheable");
            sb.Append("                ? global::DSoftStudio.Mediator.StreamPipelineChainCache<");
            sb.Append(reqType);
            sb.Append(", ");
            sb.Append(resType);
            sb.AppendLine(">.Resolve(sp)");
            sb.Append("                : global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
            sb.Append(".GetService<global::DSoftStudio.Mediator.StreamPipelineChainHandler<");
            sb.Append(reqType);
            sb.Append(", ");
            sb.Append(resType);
            sb.AppendLine(">>(sp);");

            sb.AppendLine("            if (chain is not null)");
            sb.AppendLine("                return chain.Handle(request, cancellationToken);");

            // No-behaviors fast path: resolve stream handler directly via ThreadStatic cache.
            // Null guard on Handler factory matches the InvalidOperationException contract.
            sb.Append("            var factory = global::DSoftStudio.Mediator.StreamDispatch<");
            sb.Append(reqType);
            sb.Append(", ");
            sb.Append(resType);
            sb.AppendLine(">.Handler");
            sb.Append("                ?? throw new global::System.InvalidOperationException(\"Stream handler for \" + typeof(");
            sb.Append(reqType);
            sb.AppendLine(").Name + \" not registered.\");");
            sb.Append("            return global::DSoftStudio.Mediator.StreamHandlerCache<");
            sb.Append(reqType);
            sb.Append(", ");
            sb.Append(resType);
            sb.AppendLine(">.Resolve(sp).Handle(request, cancellationToken);");

            sb.AppendLine("        }");
            sb.AppendLine();

            methodIndex++;
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    internal readonly struct InterceptCallInfo(
        string attributeSyntax,
        string requestType,
        string responseType) : System.IEquatable<InterceptCallInfo>
    {
        public string AttributeSyntax { get; } = attributeSyntax;
        public string RequestType { get; } = requestType;
        public string ResponseType { get; } = responseType;

        public bool Equals(InterceptCallInfo other) =>
            AttributeSyntax == other.AttributeSyntax;

        public override bool Equals(object obj) =>
            obj is InterceptCallInfo other && Equals(other);

        public override int GetHashCode() =>
            AttributeSyntax.GetHashCode();
    }
}
