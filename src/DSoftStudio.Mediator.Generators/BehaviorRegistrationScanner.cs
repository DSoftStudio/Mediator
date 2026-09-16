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

    /// <summary>
    /// Identity of the IServiceCollection this call registers into, as "file:declarationOffset" of the
    /// receiver symbol. One composition root has one key; a file that builds SEVERAL collections — the
    /// shape DSoftSendBenchmarks uses, three containers for the same pair — produces several, and their
    /// registrations must never be merged into one predicted chain.
    /// </summary>
    public string ReceiverKey { get; }

    public BehaviorRegistration(
        string implTypeName, bool isOpenGeneric, string requestType, string responseType,
        string filePath, int position, string receiverKey)
    {
        ImplTypeName = implTypeName;
        IsOpenGeneric = isOpenGeneric;
        RequestType = requestType;
        ResponseType = responseType;
        FilePath = filePath;
        Position = position;
        ReceiverKey = receiverKey;
    }

    public bool Equals(BehaviorRegistration other)
        => ImplTypeName == other.ImplTypeName
           && IsOpenGeneric == other.IsOpenGeneric
           && RequestType == other.RequestType
           && ResponseType == other.ResponseType
           && FilePath == other.FilePath
           && Position == other.Position
           && ReceiverKey == other.ReceiverKey;

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
            h = (h * 31) + (ReceiverKey?.GetHashCode() ?? 0);
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

    // Reuse the generator-wide formats instead of redeclaring them. BaseTypeNameFormat was
    // byte-identical to what this declared locally, and NullableFullyQualifiedFormat is the one
    // HandlerDiscovery documents as mandatory: it carries IncludeNullableReferenceTypeModifier, so a
    // nullable response renders as global::Ns.User? and not global::Ns.User.
    //
    // Declaring a different format here was wrong twice over. Emission would produce CS8631
    // nullability mismatches in the consumer's build, and MATCHING would fail silently: HandlerInfo's
    // request/response strings come from NullableFullyQualifiedFormat, so a closed registration for a
    // nullable pair would never compare equal and that pair would quietly lose its predicted chain.
    private static readonly SymbolDisplayFormat BaseNameFormat =
        ReferencedAssemblyScanner.BaseTypeNameFormat;

    private static readonly SymbolDisplayFormat FullNameFormat =
        HandlerDiscovery.NullableFullyQualifiedFormat;

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
        var receiver = ReceiverKeyOf(ctx, invocation, ct);

        return name == "AddOpenBehavior"
            ? ResolveAddOpenBehavior(ctx, invocation, method, path, pos, receiver, ct)
            : ResolveAddServiceDescriptor(ctx, invocation, method, path, pos, receiver, ct);
    }

    /// <summary>
    /// Stable identity for the receiver of the registration call — the IServiceCollection (or the
    /// MediatorBuilder, which wraps one). Uses the DECLARATION site of the symbol, so two locals both
    /// named "services" in different blocks are distinct, which is exactly the case that must not be
    /// merged. Falls back to the receiver's source text when there is no symbol to anchor to.
    /// </summary>
    private static string ReceiverKeyOf(
        GeneratorSyntaxContext ctx, InvocationExpressionSyntax invocation, CancellationToken ct)
    {
        var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
        var symbol = ctx.SemanticModel.GetSymbolInfo(receiver, ct).Symbol;

        if (symbol is not null && symbol.Locations.Length > 0)
        {
            var loc = symbol.Locations[0];
            return loc.SourceTree?.FilePath + ":" + loc.SourceSpan.Start;
        }

        return receiver.ToString();
    }

    /// <summary>
    /// <c>builder.AddOpenBehavior(typeof(B&lt;,&gt;))</c>. The receiver must be MediatorBuilder so this does
    /// not collide with unrelated extension methods of the same name.
    /// </summary>
    private static BehaviorRegistration? ResolveAddOpenBehavior(
        GeneratorSyntaxContext ctx, InvocationExpressionSyntax invocation, IMethodSymbol method,
        string path, int pos, string receiver, CancellationToken ct)
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
                impl.OriginalDefinition.ToDisplayString(BaseNameFormat), true, "", "", path, pos, receiver)
            : null;
    }

    /// <summary>
    /// The MSDI registrations: two-typeof open-generic form, and the two-type-argument closed form.
    /// </summary>
    private static BehaviorRegistration? ResolveAddServiceDescriptor(
        GeneratorSyntaxContext ctx, InvocationExpressionSyntax invocation, IMethodSymbol method,
        string path, int pos, string receiver, CancellationToken ct)
    {
        // Closed generic: AddScoped<IPipelineBehavior<Req,Res>, Impl>()
        if (method.TypeArguments.Length == 2
            && method.TypeArguments[0] is INamedTypeSymbol service
            && method.TypeArguments[1] is INamedTypeSymbol closedImpl
            && IsClosedPipelineBehavior(service, out var req, out var res)
            && IsNameable(closedImpl))
        {
            return new BehaviorRegistration(
                closedImpl.ToDisplayString(FullNameFormat), false, req, res, path, pos, receiver);
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
                    implType.OriginalDefinition.ToDisplayString(BaseNameFormat), true, "", "", path, pos, receiver)
                : null;
        }

        // Closed typeof form: AddScoped(typeof(IPipelineBehavior<Req,Res>), typeof(Impl))
        if (IsClosedPipelineBehavior(svcType, out var creq, out var cres) && IsNameable(implType))
        {
            return new BehaviorRegistration(
                implType.ToDisplayString(FullNameFormat), false, creq, cres, path, pos, receiver);
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
    /// Builds the predicted ordered chains for one (request, response) pair — one per composition
    /// root, since a file may build several IServiceCollection instances and their registrations are
    /// different chains, not one long one.
    /// <para>
    /// Grouping by receiver matters in practice: DSoftSendBenchmarks builds THREE collections for the
    /// same pair, and merging them predicted a 13-link chain that could never match any of the three.
    /// The factory returned null on every dispatch and 13 link types were emitted for nothing.
    /// </para>
    /// <para>
    /// Order within a group is source order, which is the container's order because
    /// CloseAllOpenGenericBehaviors splices closed descriptors in at the open descriptor's index.
    /// Groups whose registrations span more than one FILE are dropped: source position only orders
    /// within a file, and the relative order of two files is not something a generator can see.
    /// </para>
    /// </summary>
    public static List<List<string>> PredictChains(
        IEnumerable<BehaviorRegistration> registrations,
        string requestType,
        string responseType,
        IReadOnlyDictionary<string, BehaviorTypeInfo>? narrowedBehaviors = null)
    {
        var groups = new Dictionary<string, List<BehaviorRegistration>>();

        foreach (var r in registrations)
        {
            if (!r.IsOpenGeneric
                && (r.RequestType != requestType || r.ResponseType != responseType))
            {
                continue;
            }

            if (!groups.TryGetValue(r.ReceiverKey, out var list))
            {
                list = new List<BehaviorRegistration>();
                groups[r.ReceiverKey] = list;
            }

            list.Add(r);
        }

        var result = new List<List<string>>();

        foreach (var group in groups.Values)
        {
            if (group.Count == 0)
                continue;

            var file = group[0].FilePath;
            var singleFile = true;

            foreach (var r in group)
            {
                if (r.FilePath != file)
                {
                    singleFile = false;
                    break;
                }
            }

            if (!singleFile)
                continue;

            group.Sort(static (a, b) => a.Position.CompareTo(b.Position));

            var chain = new List<string>(group.Count);
            foreach (var r in group)
            {
                // An open-generic registration does NOT reach every pair. A behavior that narrows
                // itself with its own constraint reaches only the pairs that satisfy it — MSDI skips
                // the rest silently, and naming one of them here is CS0311 in a generated file the
                // consumer cannot edit. Registration closing already knew this; prediction did not.
                if (r.IsOpenGeneric
                    && narrowedBehaviors is not null
                    && narrowedBehaviors.TryGetValue(r.ImplTypeName, out var narrowed)
                    && !narrowed.AppliesTo(requestType, responseType))
                {
                    continue;
                }

                chain.Add(r.IsOpenGeneric
                    ? r.ImplTypeName + "<" + requestType + ", " + responseType + ">"
                    : r.ImplTypeName);
            }

            if (!AlreadyPredicted(result, chain))
                result.Add(chain);
        }

        return result;
    }

    /// <summary>
    /// True when an identical ordered chain has already been predicted for this pair.
    /// <para>
    /// Two composition roots can predict the same chain — most often because a root registers one
    /// extra behavior the generator cannot NAME (a private nested type, say), so what is left of it
    /// is character-for-character another root's chain. Emitting both produces two byte-identical
    /// factories and two full sets of link classes, and costs every OTHER root an extra failed
    /// verification before it reaches its own candidate.
    /// </para>
    /// <para>
    /// Dropping the duplicate cannot change which chains match. BehaviorChainRegistry.TryBuild
    /// verifies candidates against the RESOLVED behaviors and takes the first that passes, so two
    /// identical candidates pass on exactly the same inputs and the second could never be reached.
    /// The root whose extra behavior went unnamed still falls back, as it must — its runtime chain is
    /// longer than anything predicted for it.
    /// </para>
    /// </summary>
    private static bool AlreadyPredicted(List<List<string>> predicted, List<string> chain)
    {
        foreach (var existing in predicted)
        {
            if (existing.Count != chain.Count)
                continue;

            var same = true;
            for (int i = 0; i < chain.Count; i++)
            {
                if (!string.Equals(existing[i], chain[i], System.StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }

            if (same)
                return true;
        }

        return false;
    }
}
