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
/// Incremental generator that intercepts <c>ISender.Send&lt;TRequest, TResponse&gt;()</c> call sites
/// and replaces them with direct pipeline invocation — eliminating virtual dispatch, the
/// <c>Mediator.Send</c> method frame, and the delegate indirection on the hot path.
/// </summary>
[Generator]
public sealed class SendInterceptorGenerator : IIncrementalGenerator
{
    private const string SenderInterfaceMetadataName =
        "DSoftStudio.Mediator.Abstractions.ISender";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ── Call-site discovery (existing) ────────────────────────
        var callSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsSendCandidate(node),
                transform: static (ctx, ct) => GetInterceptInfo(ctx, ct))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var collected = callSites.Collect();

        // Combine with compilation + analyzer options (for SuppressInterceptors property).
        var collectedWithCompilation = collected
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(collectedWithCompilation, static (spc, pair) =>
        {
            var ((calls, compilation), optionsProvider) = pair;
            if (calls.IsDefaultOrEmpty)
                return;

            // Honour DSoftMediatorSuppressInterceptors MSBuild property.
            if (optionsProvider.GlobalOptions.TryGetValue(
                    "build_property.DSoftMediatorSuppressInterceptors", out var suppress)
                && string.Equals(suppress, "true", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool isRelease = compilation.Options.OptimizationLevel == OptimizationLevel.Release;
            var unique = calls.Distinct().ToList();
            var code = GenerateInterceptors(unique, isRelease);

            spc.AddSource(
                "MediatorInterceptors.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    /// <summary>
    /// Lightweight syntactic check: is this an invocation of .Send?
    /// Matches both explicit generic (.Send&lt;T,R&gt;) and type-inferred (.Send) call sites.
    /// </summary>
    private static bool IsSendCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess })
            return false;

        return memberAccess.Name switch
        {
            GenericNameSyntax { Identifier.Text: "Send", TypeArgumentList.Arguments.Count: 2 } => true,
            IdentifierNameSyntax { Identifier.Text: "Send" } => true,
            _ => false
        };
    }

    /// <summary>
    /// Semantic check: verify the call resolves to ISender.Send and extract type info + location.
    /// Uses GetInterceptableLocation API (Roslyn 4.12+) for the opaque location format.
    /// </summary>
    private static InterceptCallInfo? GetInterceptInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method)
            return null;

        if (method.Name != "Send")
            return null;

        string requestType;
        string responseType;

        if (method.TypeArguments.Length == 2)
        {
            // Explicit generic: sender.Send<Ping, int>(request)
            // Skip open-generic call sites (e.g. inside a generic forwarding method): no concrete
            // interceptor can represent them — they dispatch through Mediator.Send at runtime.
            if (InterceptorHelpers.ContainsTypeParameter(method.TypeArguments[0])
                || InterceptorHelpers.ContainsTypeParameter(method.TypeArguments[1]))
                return null;

            requestType = method.TypeArguments[0]
                .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);
            responseType = method.TypeArguments[1]
                .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);
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

        if (!InterceptorHelpers.ImplementsInterface(method.ContainingType, ctx.SemanticModel.Compilation, SenderInterfaceMetadataName))
            return null;

        // Skip call sites inside expression tree lambdas (e.g. Moq Setup/Verify,
        // NSubstitute Received). Interceptors rewrite calls to static extension
        // methods, which are incompatible with expression tree compilation.
        if (InterceptorHelpers.IsInsideExpressionTreeLambda(ctx.SemanticModel, invocation, ct))
            return null;

        // Use the new GetInterceptableLocation API (Roslyn 4.12+)
        var interceptableLocation = ctx.SemanticModel.GetInterceptableLocation(invocation, ct);
        if (interceptableLocation is null)
            return null;

        // GetInterceptsLocationAttributeSyntax returns the full attribute text:
        // [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "base64data")]
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

        // Return type is ValueTask<TResponse> — extract TResponse
        if (method.ReturnType is not INamedTypeSymbol { TypeArguments.Length: 1 } returnType)
            return false;

        // Skip open-generic call sites: an interceptor cannot reference unbound type parameters.
        if (InterceptorHelpers.ContainsTypeParameter(requestParam.Type)
            || InterceptorHelpers.ContainsTypeParameter(returnType.TypeArguments[0]))
            return false;

        requestType = requestParam.Type
            .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);

        responseType = returnType.TypeArguments[0]
            .ToDisplayString(HandlerDiscovery.NullableFullyQualifiedFormat);

        return true;
    }

    private static string GenerateInterceptors(
        List<InterceptCallInfo> calls,
        bool isRelease)
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
        sb.AppendLine("    file static class SendInterceptors");
        sb.AppendLine("    {");

        // Group by (requestType, responseType) — multiple call sites can share one method
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

            sb.Append("        internal static global::System.Threading.Tasks.ValueTask<");
            sb.Append(resType);
            sb.Append("> Send_");
            sb.Append(methodIndex);
            sb.Append("(this global::DSoftStudio.Mediator.Abstractions.ISender sender, ");
            sb.Append(reqType);
            sb.AppendLine(" request, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");

            InterceptorHelpers.AppendSendDispatchBody(sb, reqType, resType, isRelease, "            ");

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
