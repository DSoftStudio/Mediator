// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DSoftStudio.Mediator.Generators;

/// <summary>
/// ADR-0066 shared discovery + planner for the Publish fast-path tiers (the notification mirror
/// of <see cref="SendFastPath"/>). BOTH <see cref="NotificationGenerator"/> (which emits the
/// <c>__NotifCache_*</c> classes and the eligibility scan) and
/// <see cref="PublishInterceptorGenerator"/> (which routes interceptor bodies through them) run
/// this SAME code over the SAME compilation, so their per-notification-type verdicts are
/// identical by construction — the two generators cannot communicate any other way.
/// </summary>
internal static class NotificationFastPath
{
    /// <summary>Fan-out unroll cap: groups with more handlers keep today's dispatch body.</summary>
    public const int MaxUnrolledHandlers = 8;

    /// <summary>
    /// Local <c>INotificationHandler&lt;&gt;</c> implementations in this compilation — the exact
    /// filter set <c>NotificationGenerator</c> has always used, plus the ADR-0066
    /// direct-dispatch flag (a handler whose <c>Handle</c> is an explicit interface
    /// implementation or a DIM stays registered, but makes its GROUP ineligible for the
    /// concrete-typed cache: <c>concrete.Handle(...)</c> would be CS1061).
    /// </summary>
    public static NotificationHandlerEntry? GetLocalEntry(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var classDeclaration = (ClassDeclarationSyntax)ctx.Node;

        if (ctx.SemanticModel.GetDeclaredSymbol(classDeclaration, ct)
            is not INamedTypeSymbol symbol)
            return null;

        if (symbol.IsAbstract ||
            symbol.TypeKind != TypeKind.Class ||
            symbol.TypeParameters.Length > 0)
            return null;

        if (!HandlerDiscovery.IsReferenceableFromGeneratedCode(classDeclaration, symbol))
            return null;

        if (!HandlerDiscovery.TryGetNotificationHandler(
                symbol, ct, out var notificationType, out var handlerType))
            return null;

        return new NotificationHandlerEntry(
            notificationType,
            handlerType,
            HasPublicImplicitNotificationHandle(symbol));
    }

    /// <summary>
    /// True when the type's implementation of <c>INotificationHandler&lt;&gt;.Handle</c> is a
    /// public implicit member callable on the CONCRETE type (possibly inherited). Mirrors
    /// <see cref="SendFastPath.HasPublicImplicitHandle"/> for the notification interface.
    /// When <paramref name="specificInterface"/> is null, the FIRST implemented
    /// <c>INotificationHandler&lt;&gt;</c> is checked (matching
    /// <see cref="HandlerDiscovery.TryGetNotificationHandler"/>, which produced the entry).
    /// </summary>
    public static bool HasPublicImplicitNotificationHandle(
        INamedTypeSymbol symbol,
        INamedTypeSymbol? specificInterface = null)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            var original = iface.OriginalDefinition;
            if (original.ContainingNamespace?.ToDisplayString() != "DSoftStudio.Mediator.Abstractions"
                || original.MetadataName != "INotificationHandler`1")
            {
                continue;
            }

            if (specificInterface is not null
                && !SymbolEqualityComparer.Default.Equals(iface, specificInterface))
            {
                continue;
            }

            var handleMember = iface.GetMembers("Handle").OfType<IMethodSymbol>().FirstOrDefault();
            if (handleMember is null)
                return false;

            // MethodKind.Ordinary rejects explicit interface implementations; the ContainingType
            // TypeKind check rejects C# 8 default interface methods.
            return symbol.FindImplementationForInterfaceMember(handleMember) is IMethodSymbol impl
                && impl.MethodKind == MethodKind.Ordinary
                && impl.DeclaredAccessibility == Accessibility.Public
                && impl.ContainingType.TypeKind == TypeKind.Class;
        }

        return false;
    }

    /// <summary>
    /// Merges local + external entries with the SAME dedup/ordering
    /// <c>NotificationGenerator</c> has always used, grouped per notification type with the
    /// ADR-0066 cache verdict. Determinism is load-bearing: both generators must produce
    /// identical plans from identical compilations.
    /// </summary>
    public static List<NotificationCachePlan> ComputePlans(
        IEnumerable<NotificationHandlerEntry> localEntries,
        IEnumerable<NotificationHandlerEntry> externalEntries)
    {
        var merged = localEntries
            .Concat(externalEntries)
            .Distinct()
            .OrderBy(static e => e.NotificationType)
            .ThenBy(static e => e.HandlerType)
            .ToList();

        var plans = new List<NotificationCachePlan>();

        foreach (var group in merged.GroupBy(static e => e.NotificationType).OrderBy(static g => g.Key))
        {
            var handlers = group.OrderBy(static e => e.HandlerType).ToList();

            bool cacheEligible =
                handlers.Count <= MaxUnrolledHandlers
                && handlers.All(static h => h.DirectDispatchEligible);

            plans.Add(new NotificationCachePlan(
                group.Key,
                new EquatableArray<NotificationHandlerEntry>([.. handlers]),
                cacheEligible,
                "__NotifCache_" + HandlerDiscovery.SanitizeIdentifier(group.Key)));
        }

        return plans;
    }

    /// <summary>
    /// Emits the ADR-0066 per-notification-type cache class: ArmedSet (concrete readonly fields,
    /// statically-devirtualized unrolled dispatch with a NoInlining await tail preserving the
    /// sequential await-then-continue semantics of
    /// <c>NotificationCachedDispatcher.DispatchSequential</c>) + the SAFE provider-keyed
    /// <c>[ThreadStatic]</c> concrete tier whose resolve path exact-type-verifies against the
    /// generated handler set (override/decorator/subclass demotes to interface dispatch — never
    /// an InvalidCastException) and is the lazy arm point for the AGGRESSIVE holder.
    /// The class is <c>internal</c> (NOT file-local): the Publish interceptors live in a
    /// different generated file and must reference it.
    /// </summary>
    public static void AppendCacheClass(StringBuilder sb, NotificationCachePlan plan, bool emitAggressive)
    {
        var n = plan.NotificationType;
        var handlers = plan.Handlers;
        int k = handlers.Length;

        sb.Append("    internal static class ").AppendLine(plan.CacheClassName);
        sb.AppendLine("    {");

        // ── ArmedSet: the concrete handler tuple (also the SAFE tier's per-provider value). ──
        sb.AppendLine("        internal sealed class ArmedSet");
        sb.AppendLine("        {");
        for (int i = 0; i < k; i++)
            sb.Append("            internal readonly ").Append(handlers[i].HandlerType).Append(" H").Append(i).AppendLine(";");
        sb.Append("            internal ArmedSet(");
        for (int i = 0; i < k; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(handlers[i].HandlerType).Append(" h").Append(i);
        }
        sb.AppendLine(")");
        sb.AppendLine("            {");
        for (int i = 0; i < k; i++)
            sb.Append("                H").Append(i).Append(" = h").Append(i).AppendLine(";");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        if (emitAggressive)
        {
            sb.AppendLine("        private static ArmedSet? _armed;");
            sb.AppendLine();
            sb.AppendLine("        internal static ArmedSet? Armed");
            sb.AppendLine("        {");
            sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine("            get => global::System.Threading.Volatile.Read(ref _armed);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        [global::System.ThreadStatic] private static global::System.IServiceProvider? _tlsProvider;");
        sb.AppendLine("        [global::System.ThreadStatic] private static ArmedSet? _tlsSet;");
        sb.AppendLine();

        // ── Unrolled statically-devirtualized dispatch (armed AND safe-hit paths). ──
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.Append("        internal static global::System.Threading.Tasks.Task Dispatch(ArmedSet s, ")
          .Append(n).AppendLine(" notification, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("        {");
        for (int i = 0; i < k; i++)
        {
            sb.Append("            var t").Append(i).Append(" = s.H").Append(i).AppendLine(".Handle(notification, ct);");
            if (i < k - 1)
            {
                sb.Append("            if (!t").Append(i).Append(".IsCompletedSuccessfully) return AwaitTail(s, notification, ")
                  .Append(i).Append(", t").Append(i).AppendLine(", ct);");
            }
            else
            {
                sb.Append("            return t").Append(i).Append(".IsCompletedSuccessfully ? global::System.Threading.Tasks.Task.CompletedTask : t").Append(i).AppendLine(";");
            }
        }
        sb.AppendLine("        }");
        sb.AppendLine();

        if (k > 1)
        {
            // Sequential await-then-continue: handler i+1 starts only after i completes —
            // byte-for-byte the semantics of DispatchRemainingAsync.
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.Append("        private static async global::System.Threading.Tasks.Task AwaitTail(ArmedSet s, ")
              .Append(n).AppendLine(" notification, int completed, global::System.Threading.Tasks.Task pending, global::System.Threading.CancellationToken ct)");
            sb.AppendLine("        {");
            sb.AppendLine("            await pending.ConfigureAwait(false);");
            for (int i = 1; i < k; i++)
            {
                sb.Append("            if (completed < ").Append(i).AppendLine(")");
                sb.AppendLine("            {");
                sb.Append("                var t = s.H").Append(i).AppendLine(".Handle(notification, ct);");
                sb.AppendLine("                if (!t.IsCompletedSuccessfully) await t.ConfigureAwait(false);");
                sb.AppendLine("            }");
            }
            sb.AppendLine("        }");
        }
        else
        {
            // Single handler: the pending task IS the whole dispatch.
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.Append("        private static async global::System.Threading.Tasks.Task AwaitTail(ArmedSet s, ")
              .Append(n).AppendLine(" notification, int completed, global::System.Threading.Tasks.Task pending, global::System.Threading.CancellationToken ct)");
            sb.AppendLine("            => await pending.ConfigureAwait(false);");
        }
        sb.AppendLine();

        // ── SAFE tier: provider-keyed concrete cache; miss path resolves through the SHIPPED
        //    NotificationHandlerCache (keeping its per-(thread, provider) pinning envelope) and
        //    exact-type-verifies before caching concretes. ──
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.Append("        internal static global::System.Threading.Tasks.Task DispatchSafe(global::System.IServiceProvider sp, ")
          .Append(n).AppendLine(" notification, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("        {");
        sb.AppendLine("            var s = object.ReferenceEquals(_tlsProvider, sp) ? _tlsSet : null;");
        sb.AppendLine("            if (s is not null)");
        sb.AppendLine("                return Dispatch(s, notification, ct);");
        sb.AppendLine("            return ResolveSlow(sp, notification, ct);");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.Append("        private static global::System.Threading.Tasks.Task ResolveSlow(global::System.IServiceProvider sp, ")
          .Append(n).AppendLine(" notification, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("        {");
        sb.Append("            var factories = global::DSoftStudio.Mediator.NotificationDispatch<").Append(n).AppendLine(">.Handlers;");
        sb.AppendLine("            if (factories is null || factories.Length == 0)");
        sb.AppendLine("                return global::System.Threading.Tasks.Task.CompletedTask;");
        sb.Append("            var handlers = global::DSoftStudio.Mediator.NotificationHandlerCache<").Append(n).AppendLine(">.Resolve(sp, factories);");
        // Same rule as the Send concrete tier: NotificationHandlerCache.Resolve has already decided
        // whether this container lets the array be reused (every handler non-Transient), so read that
        // verdict rather than re-deriving it. Without it the ArmedSet pinned Transient handlers.
        sb.Append("            if (global::DSoftStudio.Mediator.NotificationHandlerCache<").Append(n)
          .AppendLine(">.IsCacheableFor(sp)");
        sb.Append("                && handlers.Length == ").Append(k);
        for (int i = 0; i < k; i++)
        {
            sb.AppendLine();
            sb.Append("                && handlers[").Append(i).Append("].GetType() == typeof(").Append(handlers[i].HandlerType).Append(')');
        }
        sb.AppendLine(")");
        sb.AppendLine("            {");
        sb.Append("                var set = new ArmedSet(");
        for (int i = 0; i < k; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('(').Append(handlers[i].HandlerType).Append(")handlers[").Append(i).Append(']');
        }
        sb.AppendLine(");");
        sb.AppendLine("                _tlsProvider = sp;");
        sb.AppendLine("                _tlsSet = set;");
        if (emitAggressive)
        {
            sb.Append("                if (global::DSoftStudio.Mediator.AggressiveNotificationDispatch<").Append(n).AppendLine(">.ShouldAttemptArm)");
            sb.AppendLine("                {");
            sb.Append("                    global::DSoftStudio.Mediator.AggressiveNotificationDispatch<").Append(n).AppendLine(">.TryArm(");
            sb.Append("                        new object[] { ");
            for (int i = 0; i < k; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("handlers[").Append(i).Append(']');
            }
            sb.AppendLine(" },");
            sb.AppendLine("                        () => global::System.Threading.Volatile.Write(ref _armed, set),");
            sb.AppendLine("                        static () => global::System.Threading.Volatile.Write(ref _armed, null));");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                return Dispatch(set, notification, ct);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Override/decorator/subclass, or handlers this container registered Transient:");
        sb.AppendLine("            // interface dispatch over the array ALREADY resolved above. Passing it on matters —");
        sb.AppendLine("            // the (notification, sp, ct) overload would resolve again, and for a non-reusable");
        sb.AppendLine("            // array that means constructing every handler a second time for one Publish.");
        sb.AppendLine("            return global::DSoftStudio.Mediator.NotificationCachedDispatcher.DispatchSequential(handlers, notification, ct);");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine();
    }
}

/// <summary>A discovered notification handler with its ADR-0066 direct-dispatch verdict.</summary>
internal readonly struct NotificationHandlerEntry(
    string notificationType,
    string handlerType,
    bool directDispatchEligible) : System.IEquatable<NotificationHandlerEntry>
{
    public string NotificationType { get; } = notificationType;
    public string HandlerType { get; } = handlerType;
    public bool DirectDispatchEligible { get; } = directDispatchEligible;

    public bool Equals(NotificationHandlerEntry other) =>
        NotificationType == other.NotificationType
        && HandlerType == other.HandlerType
        && DirectDispatchEligible == other.DirectDispatchEligible;

    public override bool Equals(object obj) =>
        obj is NotificationHandlerEntry other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = NotificationType?.GetHashCode() ?? 0;
            hash = (hash * 397) ^ (HandlerType?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (DirectDispatchEligible ? 1 : 0);
            return hash;
        }
    }
}

/// <summary>Per-notification-type plan shared by both Publish-side generators.</summary>
internal readonly struct NotificationCachePlan(
    string notificationType,
    EquatableArray<NotificationHandlerEntry> handlers,
    bool cacheEligible,
    string cacheClassName) : System.IEquatable<NotificationCachePlan>
{
    public string NotificationType { get; } = notificationType;
    public EquatableArray<NotificationHandlerEntry> Handlers { get; } = handlers;
    /// <summary>True when the concrete-typed cache class is emitted for this type.</summary>
    public bool CacheEligible { get; } = cacheEligible;
    public string CacheClassName { get; } = cacheClassName;

    public bool Equals(NotificationCachePlan other) =>
        NotificationType == other.NotificationType
        && CacheEligible == other.CacheEligible
        && CacheClassName == other.CacheClassName
        && Handlers.Equals(other.Handlers);

    public override bool Equals(object obj) =>
        obj is NotificationCachePlan other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = NotificationType?.GetHashCode() ?? 0;
            hash = (hash * 397) ^ (CacheEligible ? 1 : 0);
            hash = (hash * 397) ^ Handlers.GetHashCode();
            return hash;
        }
    }
}
