// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// One recognized pipeline-behavior registration call site, carrying enough to reconstruct the
/// ordered chain the container will end up with.
/// <para>
/// <see cref="FilePath"/> and <see cref="Position"/> exist because behaviors execute in REGISTRATION
/// order and the only order visible to a generator is source order. That is a usable proxy only
/// because <c>CloseAllOpenGenericBehaviors</c> now splices closed-generic descriptors in at the open
/// descriptor's own index; while it appended them, an open-generic behavior always ended up innermost
/// regardless of where it was written, and no source-order prediction could have matched.
/// </para>
/// </summary>
internal readonly struct BehaviorRegistration : System.IEquatable<BehaviorRegistration>
{
    /// <summary>
    /// The implementation type. For an OPEN-generic registration this is the base name without generic
    /// arguments (<c>global::App.LoggingBehavior</c>), to be closed over each pair. For a CLOSED
    /// registration it is already the full type name and is used verbatim.
    /// </summary>
    public string ImplTypeName { get; }

    /// <summary><see langword="true"/> when registered as an open generic and therefore applies to every pair.</summary>
    public bool IsOpenGeneric { get; }

    /// <summary>Request type when registered closed; empty for open-generic registrations.</summary>
    public string RequestType { get; }

    /// <summary>Response type when registered closed; empty for open-generic registrations.</summary>
    public string ResponseType { get; }

    public string FilePath { get; }

    public int Position { get; }

    public BehaviorRegistration(
        string implTypeName, bool isOpenGeneric, string requestType, string responseType,
        string filePath, int position)
    {
        ImplTypeName = implTypeName;
        IsOpenGeneric = isOpenGeneric;
        RequestType = requestType;
        ResponseType = responseType;
        FilePath = filePath;
        Position = position;
    }

    public bool Equals(BehaviorRegistration other)
        => ImplTypeName == other.ImplTypeName
           && IsOpenGeneric == other.IsOpenGeneric
           && RequestType == other.RequestType
           && ResponseType == other.ResponseType
           && FilePath == other.FilePath
           && Position == other.Position;

    public override bool Equals(object obj) => obj is BehaviorRegistration o && Equals(o);

    public override int GetHashCode()
    {
        unchecked
        {
            var h = 17;
            h = (h * 31) + (ImplTypeName?.GetHashCode() ?? 0);
            h = (h * 31) + IsOpenGeneric.GetHashCode();
            h = (h * 31) + (RequestType?.GetHashCode() ?? 0);
            h = (h * 31) + (ResponseType?.GetHashCode() ?? 0);
            h = (h * 31) + (FilePath?.GetHashCode() ?? 0);
            h = (h * 31) + Position;
            return h;
        }
    }
}

/// <summary>
/// Recognizes pipeline-behavior registration call sites so the generator can PREDICT the ordered
/// chain for a (request, response) pair and emit a fully specialized chain for it.
/// <para>
/// The prediction is never load-bearing. Anything not recognized here simply produces no prediction
/// for the affected pair, and that pair keeps today's per-link construction. Even a recognized-but-
/// wrong prediction is harmless: the generated factory re-verifies the resolved instances at chain
/// construction and returns null on any mismatch. So this scanner is free to be incomplete, and must
/// never guess.
/// </para>
/// <para>
/// Three shapes are recognized, covering the dominant composition-root patterns:
/// </para>
/// <list type="number">
/// <item><description><c>services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(B&lt;,&gt;))</c> and the
/// Singleton/Transient variants — open generic, applies to every pair.</description></item>
/// <item><description><c>services.AddScoped&lt;IPipelineBehavior&lt;Req,Res&gt;, Impl&gt;()</c> — closed, one pair.</description></item>
/// <item><description><c>builder.AddOpenBehavior(typeof(B&lt;,&gt;))</c> — the form the docs recommend, readable
/// only when the lambda is written inline at the AddMediator call site, because the generated
/// AddMediator body just invokes the delegate.</description></item>
/// </list>
/// <para>
/// Deliberately NOT recognized, because they cannot be read from syntax: registrations inside a
/// referenced assembly's method body (Roslyn does not carry method bodies in metadata — this is why
/// the FluentValidation / HybridCache / OpenTelemetry packages are opaque), registrations guarded by
/// runtime configuration, and factory-lambda registrations whose <c>ImplementationType</c> is null.
/// </para>
/// </summary>
internal static class BehaviorRegistrationScanner
{
    private const string AbstractionsNamespace = "DSoftStudio.Mediator.Abstractions";
    private const string PipelineBehaviorMetadataName = "IPipelineBehavior`2";

    private static readonly SymbolDisplayFormat BaseNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None);

    private static readonly SymbolDisplayFormat FullNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>Cheap syntactic pre-filter for the incremental pipeline's predicate.</summary>
    public static bool IsCandidate(SyntaxNode node)
        => node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }
           && member.Name.Identifier.ValueText is
               "AddScoped" or "AddSingleton" or "AddTransient" or "AddOpenBehavior";

    /// <summary>
    /// Resolves one candidate to a <see cref="BehaviorRegistration"/>, or null when it is not a
    /// pipeline-behavior registration this scanner can read.
    /// </summary>
    public static BehaviorRegistration? Resolve(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var name = ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.ValueText;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method)
            return null;

        var path = invocation.SyntaxTree.FilePath;
        var pos = invocation.SpanStart;

        return name == "AddOpenBehavior"
            ? ResolveAddOpenBehavior(ctx, invocation, method, path, pos, ct)
            : ResolveAddServiceDescriptor(ctx, invocation, method, path, pos, ct);
    }

    /// <summary>
    /// <c>builder.AddOpenBehavior(typeof(B&lt;,&gt;))</c>. The receiver must be MediatorBuilder so this does
    /// not collide with unrelated extension methods of the same name.
    /// </summary>
    private static BehaviorRegistration? ResolveAddOpenBehavior(
        GeneratorSyntaxContext ctx, InvocationExpressionSyntax invocation, IMethodSymbol method,
        string path, int pos, CancellationToken ct)
    {
        if (method.ContainingType?.ToDisplayString() != "DSoftStudio.Mediator.MediatorBuilder")
            return null;

        if (invocation.ArgumentList.Arguments.Count != 1)
            return null;

        var impl = TypeOfArgument(ctx, invocation.ArgumentList.Arguments[0], ct);
        if (impl is null || !impl.IsUnboundGenericType)
            return null;

        return ImplementsPipelineBehavior(impl.OriginalDefinition) && IsNameable(impl.OriginalDefinition)
            ? new BehaviorRegistration(
                impl.OriginalDefinition.ToDisplayString(BaseNameFormat), true, "", "", path, pos)
            : null;
    }

    /// <summary>
    /// The MSDI registrations: two-typeof open-generic form, and the two-type-argument closed form.
    /// </summary>
    private static BehaviorRegistration? ResolveAddServiceDescriptor(
        GeneratorSyntaxContext ctx, InvocationExpressionSyntax invocation, IMethodSymbol method,
        string path, int pos, CancellationToken ct)
    {
        // Closed generic: AddScoped<IPipelineBehavior<Req,Res>, Impl>()
        if (method.TypeArguments.Length == 2
            && method.TypeArguments[0] is INamedTypeSymbol service
            && method.TypeArguments[1] is INamedTypeSymbol closedImpl
            && IsClosedPipelineBehavior(service, out var req, out var res)
            && IsNameable(closedImpl))
        {
            return new BehaviorRegistration(
                closedImpl.ToDisplayString(FullNameFormat), false, req, res, path, pos);
        }

        // Open generic: AddScoped(typeof(IPipelineBehavior<,>), typeof(Impl<,>))
        if (invocation.ArgumentList.Arguments.Count != 2)
            return null;

        var svcType = TypeOfArgument(ctx, invocation.ArgumentList.Arguments[0], ct);
        var implType = TypeOfArgument(ctx, invocation.ArgumentList.Arguments[1], ct);

        if (svcType is null || implType is null)
            return null;

        // Open form: both unbound.
        if (svcType.IsUnboundGenericType && implType.IsUnboundGenericType)
        {
            return IsPipelineBehaviorDefinition(svcType.OriginalDefinition)
                   && ImplementsPipelineBehavior(implType.OriginalDefinition)
                   && IsNameable(implType.OriginalDefinition)
                ? new BehaviorRegistration(
                    implType.OriginalDefinition.ToDisplayString(BaseNameFormat), true, "", "", path, pos)
                : null;
        }

        // Closed typeof form: AddScoped(typeof(IPipelineBehavior<Req,Res>), typeof(Impl))
        if (IsClosedPipelineBehavior(svcType, out var creq, out var cres) && IsNameable(implType))
        {
            return new BehaviorRegistration(
                implType.ToDisplayString(FullNameFormat), false, creq, cres, path, pos);
        }

        return null;
    }

    private static INamedTypeSymbol? TypeOfArgument(
        GeneratorSyntaxContext ctx, ArgumentSyntax argument, CancellationToken ct)
        => argument.Expression is TypeOfExpressionSyntax typeOf
           && ctx.SemanticModel.GetTypeInfo(typeOf.Type, ct).Type is INamedTypeSymbol named
            ? named
            : null;

    private static bool IsPipelineBehaviorDefinition(ITypeSymbol type)
        => type is INamedTypeSymbol named
           && named.MetadataName == PipelineBehaviorMetadataName
           && named.ContainingNamespace?.ToDisplayString() == AbstractionsNamespace;

    private static bool IsClosedPipelineBehavior(
        INamedTypeSymbol type, out string requestType, out string responseType)
    {
        requestType = "";
        responseType = "";

        if (!IsPipelineBehaviorDefinition(type.OriginalDefinition)
            || type.IsUnboundGenericType
            || type.TypeArguments.Length != 2)
        {
            return false;
        }

        requestType = type.TypeArguments[0].ToDisplayString(FullNameFormat);
        responseType = type.TypeArguments[1].ToDisplayString(FullNameFormat);
        return true;
    }

    /// <summary>
    /// The generated chain links name the behavior type in a FIELD, so the type has to be spellable
    /// from generated code in this assembly. A private nested behavior (a common shape in test and
    /// benchmark fixtures) is not, and emitting it produces CS0122 in the consumer's build. Reuses the
    /// same rule as behavior discovery, including the enclosing-generic exclusion.
    /// </summary>
    private static bool IsNameable(INamedTypeSymbol type)
        => ReferencedAssemblyScanner.IsNameableBehaviorType(type, allowInternal: true);

    private static bool ImplementsPipelineBehavior(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (IsPipelineBehaviorDefinition(iface.OriginalDefinition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds the predicted ordered chain for one (request, response) pair, or null when no prediction
    /// should be made.
    /// <para>
    /// Open-generic registrations apply to every pair; closed ones only to their own. Order is source
    /// order, which is the container's order because the closure splices in place.
    /// </para>
    /// <para>
    /// Bails out (returns null, so the pair keeps per-link construction) when the registrations that
    /// would form the chain are spread across MORE THAN ONE FILE. Source position only orders within a
    /// file, and the relative order of two composition-root files is a build-time fact this generator
    /// cannot see. A composition root in one file is the overwhelmingly common shape; anything else
    /// simply does not get the specialized tier.
    /// </para>
    /// </summary>
    public static List<string>? PredictChain(
        IEnumerable<BehaviorRegistration> registrations,
        string requestType,
        string responseType)
    {
        var applicable = new List<BehaviorRegistration>();

        foreach (var r in registrations)
        {
            if (r.IsOpenGeneric
                || (r.RequestType == requestType && r.ResponseType == responseType))
            {
                applicable.Add(r);
            }
        }

        if (applicable.Count == 0)
            return null;

        var file = applicable[0].FilePath;
        foreach (var r in applicable)
        {
            if (r.FilePath != file)
                return null;
        }

        applicable.Sort(static (a, b) => a.Position.CompareTo(b.Position));

        var chain = new List<string>(applicable.Count);
        foreach (var r in applicable)
        {
            chain.Add(r.IsOpenGeneric
                ? r.ImplTypeName + "<" + requestType + ", " + responseType + ">"
                : r.ImplTypeName);
        }

        return chain;
    }
}
