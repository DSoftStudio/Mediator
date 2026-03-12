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
/// Incremental generator that intercepts <c>IPublisher.Publish&lt;TNotification&gt;()</c>
/// call sites and replaces them with direct cached dispatch — eliminating virtual dispatch,
/// the <c>Mediator.Publish</c> method frame, and per-call DI resolution.
/// </summary>
[Generator]
public sealed class PublishInterceptorGenerator : IIncrementalGenerator
{
    private const string PublisherInterfaceMetadataName =
        "DSoftStudio.Mediator.Abstractions.IPublisher";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var callSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsPublishCandidate(node),
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
                "PublishInterceptors.g.cs",
                SourceText.From(code, Encoding.UTF8));
        });
    }

    /// <summary>
    /// Lightweight syntactic check: is this an invocation of .Publish?
    /// Matches both explicit generic (.Publish&lt;T&gt;) and type-inferred (.Publish) call sites.
    /// Excludes .Publish(object) overloads (no type arguments, single non-generic param).
    /// </summary>
    private static bool IsPublishCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess })
            return false;

        return memberAccess.Name switch
        {
            GenericNameSyntax { Identifier.Text: "Publish", TypeArgumentList.Arguments.Count: 1 } => true,
            IdentifierNameSyntax { Identifier.Text: "Publish" } => true,
            _ => false
        };
    }

    /// <summary>
    /// Semantic check: verify the call resolves to IPublisher.Publish&lt;T&gt; and extract type info.
    /// Handles both explicit generic and type-inferred extension method calls.
    /// Excludes the non-generic Publish(object) overload.
    /// </summary>
    private static InterceptCallInfo? GetInterceptInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method)
            return null;

        if (method.Name != "Publish")
            return null;

        string notificationType;

        if (method.TypeArguments.Length == 1)
        {
            // Explicit generic: publisher.Publish<PingNotification>(notification)
            notificationType = method.TypeArguments[0]
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        else if (method.TypeArguments.Length == 0 && method.Parameters.Length >= 1)
        {
            if (!TryResolveInferredNotificationType(method, ctx.SemanticModel.Compilation, out notificationType))
                return null;
        }
        else
        {
            return null;
        }

        if (!InterceptorHelpers.ImplementsInterface(method.ContainingType, ctx.SemanticModel.Compilation, PublisherInterfaceMetadataName))
            return null;

        var interceptableLocation = ctx.SemanticModel.GetInterceptableLocation(invocation, ct);
        if (interceptableLocation is null)
            return null;

        var attributeSyntax = interceptableLocation.GetInterceptsLocationAttributeSyntax();

        return new InterceptCallInfo(
            attributeSyntax: attributeSyntax,
            notificationType: notificationType);
    }

    private static bool TryResolveInferredNotificationType(
        IMethodSymbol method,
        Compilation compilation,
        out string notificationType)
    {
        notificationType = string.Empty;

        var notificationParam = InterceptorHelpers.ResolveRequestParameter(method);
        if (notificationParam is null)
            return false;

        var paramType = notificationParam.Type;

        // Verify the parameter type implements INotification (excludes Publish(object) overload)
        if (paramType is not INamedTypeSymbol namedParamType)
            return false;

        if (!InterceptorHelpers.ImplementsInterface(namedParamType, compilation, "DSoftStudio.Mediator.Abstractions.INotification"))
            return false;

        notificationType = paramType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return true;
    }

    private static string GenerateInterceptors(List<InterceptCallInfo> calls)
    {
        var sb = new StringBuilder(2048);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS9113 // Parameter is unread (required by compiler for interceptor attribute)");
        sb.AppendLine();

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
        sb.AppendLine("    file static class PublishInterceptors");
        sb.AppendLine("    {");

        var groups = calls
            .GroupBy(c => c.NotificationType)
            .ToList();

        int methodIndex = 0;
        foreach (var group in groups)
        {
            var notifType = group.Key;

            foreach (var call in group)
            {
                sb.Append("        ");
                sb.AppendLine(call.AttributeSyntax);
            }

            sb.Append("        internal static global::System.Threading.Tasks.Task Publish_");
            sb.Append(methodIndex);
            sb.Append("(this global::DSoftStudio.Mediator.Abstractions.IPublisher publisher, ");
            sb.Append(notifType);
            sb.AppendLine(" notification, global::System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(notification);");
            sb.AppendLine("            var sp = ((global::DSoftStudio.Mediator.IServiceProviderAccessor)publisher).ServiceProvider;");

            // Custom publisher path (rare — only when INotificationPublisher is registered)
            sb.AppendLine("            var customPublisher = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
            sb.AppendLine("                .GetService<global::DSoftStudio.Mediator.Abstractions.INotificationPublisher>(sp);");
            sb.AppendLine("            if (customPublisher is not null)");
            sb.AppendLine("            {");
            sb.Append("                var handlers = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions");
            sb.Append(".GetServices<global::DSoftStudio.Mediator.Abstractions.INotificationHandler<");
            sb.Append(notifType);
            sb.AppendLine(">>(sp);");
            sb.AppendLine("                return customPublisher.Publish(handlers, notification, cancellationToken);");
            sb.AppendLine("            }");

            // Default sequential dispatch with ThreadStatic cached handlers
            sb.Append("            return global::DSoftStudio.Mediator.NotificationCachedDispatcher.DispatchSequential(notification, sp, cancellationToken);");
            sb.AppendLine();

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
        string notificationType) : System.IEquatable<InterceptCallInfo>
    {
        public string AttributeSyntax { get; } = attributeSyntax;
        public string NotificationType { get; } = notificationType;

        public bool Equals(InterceptCallInfo other) =>
            AttributeSyntax == other.AttributeSyntax;

        public override bool Equals(object obj) =>
            obj is InterceptCallInfo other && Equals(other);

        public override int GetHashCode() =>
            AttributeSyntax.GetHashCode();
    }
}
