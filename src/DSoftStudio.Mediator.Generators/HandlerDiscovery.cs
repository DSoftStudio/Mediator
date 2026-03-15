// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DSoftStudio.Mediator.Generators
{
    internal static class HandlerDiscovery
    {
        private const string RequestHandlerMetadataName =
            "DSoftStudio.Mediator.Abstractions.IRequestHandler`2";

        private const string NotificationHandlerMetadataName =
            "DSoftStudio.Mediator.Abstractions.INotificationHandler`1";

        private const string StreamHandlerMetadataName =
            "DSoftStudio.Mediator.Abstractions.IStreamRequestHandler`2";

        private const string RequestMetadataName =
            "DSoftStudio.Mediator.Abstractions.IRequest`1";


        public static bool TryGetRequestHandler(
            INamedTypeSymbol symbol,
            CancellationToken ct,
            out string requestType,
            out string responseType)
        {
            requestType = string.Empty;
            responseType = string.Empty;    
            foreach (var iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();

                if (!IsTargetInterface(iface, RequestHandlerMetadataName))
                    continue;

                requestType = iface.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                responseType = iface.TypeArguments[1]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                return true;
            }

            return false;
        }


        public static bool TryGetNotificationHandler(
            INamedTypeSymbol symbol,
            CancellationToken ct,
            out string notificationType,
            out string handlerType)
        {
            notificationType = string.Empty;
            handlerType = string.Empty;

            foreach (var iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();

                if (!IsTargetInterface(iface, NotificationHandlerMetadataName))
                    continue;

                notificationType = iface.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                handlerType = symbol
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                return true;
            }

            return false;
        }


        public static bool TryGetStreamHandler(
            INamedTypeSymbol symbol,
            CancellationToken ct,
            out string requestType,
            out string responseType,
            out string handlerType)
        {
            requestType = string.Empty;
            responseType = string.Empty;
            handlerType = string.Empty;

            foreach (var iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();

                if (!IsTargetInterface(iface, StreamHandlerMetadataName))
                    continue;

                requestType = iface.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                responseType = iface.TypeArguments[1]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                handlerType = symbol
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                return true;
            }

            return false;
        }


        /// <summary>
        /// Returns <c>true</c> when the type has the <c>file</c> modifier (C# 11+).
        /// File-local types cannot be referenced from generated code and must be skipped.
        /// </summary>
        public static bool IsFileLocal(TypeDeclarationSyntax typeDecl)
        {
            foreach (var modifier in typeDecl.Modifiers)
            {
                if (modifier.Text == "file")
                    return true;
            }
            return false;
        }

        // ── Self-handling request discovery ──────────────────────────────

        /// <summary>
        /// Detects a "self-handling" request class: implements <c>IRequest&lt;T&gt;</c>
        /// (or <c>ICommand&lt;T&gt;</c> / <c>IQuery&lt;T&gt;</c>) and contains a
        /// <c>static Execute</c> method. Returns <see langword="false"/> when the
        /// class also implements <c>IRequestHandler&lt;,&gt;</c> (normal pattern).
        /// </summary>
        public static bool TryGetSelfHandlingRequest(
            INamedTypeSymbol symbol,
            CancellationToken ct,
            out SelfHandlerDetail detail)
        {
            detail = default;

            // 1. Find IRequest<TResponse> in the interface list
            string responseType = null;
            foreach (var iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsTargetInterface(iface, RequestMetadataName))
                    continue;
                responseType = iface.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                break;
            }

            if (responseType == null)
                return false;

            // 2. Must NOT also implement IRequestHandler (normal handler takes precedence)
            foreach (var iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();
                if (IsTargetInterface(iface, RequestHandlerMetadataName))
                    return false;
            }

            // 3. Find a static Execute method
            IMethodSymbol executeMethod = null;
            foreach (var member in symbol.GetMembers("Execute"))
            {
                if (member is IMethodSymbol m && m.IsStatic && !m.IsAbstract)
                {
                    executeMethod = m;
                    break;
                }
            }

            if (executeMethod == null)
                return false;

            var requestType = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // 4. Analyze return type
            int returnKind = AnalyzeReturnKind(executeMethod, responseType);
            if (returnKind < 0)
                return false;

            // 5. Analyze parameters
            var parameters = new List<SelfHandlerParam>();
            foreach (var param in executeMethod.Parameters)
            {
                if (SymbolEqualityComparer.Default.Equals(param.Type, symbol))
                {
                    parameters.Add(new SelfHandlerParam(
                        SelfHandlerParam.KindRequest, requestType));
                }
                else if (param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                         == "global::System.Threading.CancellationToken")
                {
                    parameters.Add(new SelfHandlerParam(
                        SelfHandlerParam.KindCancellationToken,
                        "global::System.Threading.CancellationToken"));
                }
                else
                {
                    var paramType = param.Type
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    parameters.Add(new SelfHandlerParam(
                        SelfHandlerParam.KindService, paramType));
                }
            }

            detail = new SelfHandlerDetail(
                requestType,
                responseType,
                returnKind,
                new EquatableArray<SelfHandlerParam>(parameters.ToArray()));

            return true;
        }

        /// <summary>
        /// Analyzes the return type of an Execute method.
        /// Returns one of the <see cref="SelfHandlerDetail"/> return-kind constants,
        /// or <c>-1</c> when the return type does not match TResponse.
        /// </summary>
        private static int AnalyzeReturnKind(
            IMethodSymbol method,
            string expectedResponseType)
        {
            var returnType = method.ReturnType;

            // void → must be Unit response
            if (returnType.SpecialType == SpecialType.System_Void)
                return expectedResponseType ==
                       "global::DSoftStudio.Mediator.Abstractions.Unit"
                    ? SelfHandlerDetail.ReturnVoid
                    : -1;

            var returnTypeName = returnType
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Task (non-generic) → Unit response
            if (returnTypeName == "global::System.Threading.Tasks.Task")
                return expectedResponseType ==
                       "global::DSoftStudio.Mediator.Abstractions.Unit"
                    ? SelfHandlerDetail.ReturnTask
                    : -1;

            // Sync: T matches TResponse directly
            if (returnTypeName == expectedResponseType)
                return SelfHandlerDetail.ReturnSync;

            // Task<T> or ValueTask<T>
            if (returnType is INamedTypeSymbol { IsGenericType: true } named
                && named.TypeArguments.Length == 1)
            {
                var inner = named.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                if (inner == expectedResponseType)
                {
                    var ns = named.OriginalDefinition
                        .ContainingNamespace?.ToDisplayString();
                    var meta = named.OriginalDefinition.MetadataName;

                    if (ns == "System.Threading.Tasks")
                    {
                        if (meta == "Task`1")
                            return SelfHandlerDetail.ReturnTaskOfT;
                        if (meta == "ValueTask`1")
                            return SelfHandlerDetail.ReturnValueTaskOfT;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Converts a fully qualified type name (e.g. <c>global::Ns.User.Login</c>)
        /// to a valid C# identifier for use as adapter class names in generated code.
        /// </summary>
        public static string SanitizeIdentifier(string fullyQualifiedName)
        {
            var name = fullyQualifiedName;
            if (name.StartsWith("global::"))
                name = name.Substring(8);

            var sb = new StringBuilder(name.Length + 1);
            foreach (var c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

            if (sb.Length == 0 || (!char.IsLetter(sb[0]) && sb[0] != '_'))
                sb.Insert(0, '_');

            return sb.ToString();
        }

        private static bool IsTargetInterface(
            INamedTypeSymbol iface,
            string metadataName)
        {
            var original = iface.OriginalDefinition;
            var qualifiedName = original.ContainingNamespace.ToDisplayString()
                + "." + original.MetadataName;
            return qualifiedName == metadataName;
        }
    }

    // ── Data structures for self-handling request discovery ──────────────

    /// <summary>
    /// Identifies a parameter in a self-handling Execute method.
    /// </summary>
    internal readonly struct SelfHandlerParam : System.IEquatable<SelfHandlerParam>
    {
        public const int KindRequest = 0;
        public const int KindCancellationToken = 1;
        public const int KindService = 2;

        public int Kind { get; }
        public string TypeName { get; }

        public SelfHandlerParam(int kind, string typeName)
        {
            Kind = kind;
            TypeName = typeName;
        }

        public bool Equals(SelfHandlerParam other) =>
            Kind == other.Kind && TypeName == other.TypeName;

        public override bool Equals(object obj) =>
            obj is SelfHandlerParam other && Equals(other);

        public override int GetHashCode()
        {
            unchecked { return (Kind * 397) ^ (TypeName?.GetHashCode() ?? 0); }
        }
    }

    /// <summary>
    /// Full description of a self-handling request class discovered by
    /// <see cref="HandlerDiscovery.TryGetSelfHandlingRequest"/>.
    /// Contains everything the DI generator needs to emit an adapter class.
    /// </summary>
    internal readonly struct SelfHandlerDetail : System.IEquatable<SelfHandlerDetail>
    {
        /// <summary>Execute returns T (sync).</summary>
        public const int ReturnSync = 0;
        /// <summary>Execute returns Task&lt;T&gt;.</summary>
        public const int ReturnTaskOfT = 1;
        /// <summary>Execute returns ValueTask&lt;T&gt;.</summary>
        public const int ReturnValueTaskOfT = 2;
        /// <summary>Execute returns void (Unit response).</summary>
        public const int ReturnVoid = 3;
        /// <summary>Execute returns Task (Unit response, async).</summary>
        public const int ReturnTask = 4;

        public string RequestType { get; }
        public string ResponseType { get; }
        public int ReturnKind { get; }
        public EquatableArray<SelfHandlerParam> Parameters { get; }

        public SelfHandlerDetail(
            string requestType,
            string responseType,
            int returnKind,
            EquatableArray<SelfHandlerParam> parameters)
        {
            RequestType = requestType;
            ResponseType = responseType;
            ReturnKind = returnKind;
            Parameters = parameters;
        }

        public bool Equals(SelfHandlerDetail other) =>
            RequestType == other.RequestType &&
            ResponseType == other.ResponseType &&
            ReturnKind == other.ReturnKind &&
            Parameters.Equals(other.Parameters);

        public override bool Equals(object obj) =>
            obj is SelfHandlerDetail other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RequestType?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ (ResponseType?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ ReturnKind;
                hash = (hash * 397) ^ Parameters.GetHashCode();
                return hash;
            }
        }
    }
}
