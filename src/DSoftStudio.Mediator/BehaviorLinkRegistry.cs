// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Per-(request, response) map from a behavior's CONCRETE type to a generated chain-link factory.
    /// <para>
    /// <b>Why.</b> <see cref="BehaviorHandlerAdapter{TRequest, TResponse}"/> stores the behavior in an
    /// <see cref="IPipelineBehavior{TRequest, TResponse}"/> field, so every link costs an interface
    /// dispatch into the behavior on top of the interface dispatch the behavior itself makes through
    /// its <c>next</c> parameter — two per link. A generated link stores the behavior in a field typed
    /// to its concrete sealed class, which binds that first call statically and lets the JIT inline it.
    /// </para>
    /// <para>
    /// <b>What it does not do.</b> The <c>next</c> parameter stays interface-typed, because the chain
    /// order is a DI registration fact and is not known when the code is generated. Measured on
    /// .NET 11 preview 7 with three distinct pass-through behaviors, per-link cost: interface+interface
    /// 3.08 ns, concrete behavior + interface next 1.24 ns, concrete behavior + concrete next 0.10 ns.
    /// This type buys the middle number. Reaching the last one requires knowing the exact chain at
    /// compile time, which needs registration-site analysis and a fail-open verifier — a separate tier
    /// that would build on top of this one.
    /// </para>
    /// <para>
    /// <b>Correctness.</b> The lookup is an EXACT type match, never <c>is</c>. A subclass that hides
    /// <c>Handle</c> with <c>new</c>, or a decorator wrapping the behavior, must NOT be routed through
    /// a link typed to the base class — that would statically bind the base implementation and silently
    /// misdispatch. Anything not matched exactly falls back to today's interface-typed adapter. Same
    /// discipline as the ADR-0065 concrete handler caches.
    /// </para>
    /// <para>
    /// <b>Cost.</b> <see cref="Link"/> runs from the <see cref="PipelineChainHandler{TRequest, TResponse}"/>
    /// constructor — once per DI scope, never on the dispatch path.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class BehaviorLinkRegistry<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Builds one generated chain link. <c>behavior</c> is guaranteed by the registry lookup to be
        /// exactly the concrete type the link expects, so the cast inside the generated factory is safe.
        /// </summary>
        public delegate IRequestHandler<TRequest, TResponse> LinkFactory(
            IPipelineBehavior<TRequest, TResponse> behavior,
            IRequestHandler<TRequest, TResponse> next);

        // Written only during registration (PrecompilePipelines / AddMediator(configure)), read from
        // scope construction. Published with Volatile so a reader either sees no table or a fully
        // built one; the dictionary itself is never mutated after publication.
        private static Dictionary<Type, LinkFactory>? _factories;

        /// <summary>
        /// Registers the generated link factory for one concrete behavior type. Called from generated
        /// registration code. Last registration wins, which matters only if a type is registered twice.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Register(Type behaviorType, LinkFactory factory)
        {
            var current = Volatile.Read(ref _factories);
            var next = current is null
                ? new Dictionary<Type, LinkFactory>()
                : new Dictionary<Type, LinkFactory>(current);

            next[behaviorType] = factory;
            Volatile.Write(ref _factories, next);
        }

        /// <summary>
        /// Returns the chain link wrapping <paramref name="behavior"/> around <paramref name="next"/>:
        /// the generated concrete-typed link when one exists for the behavior's EXACT runtime type,
        /// otherwise today's interface-typed adapter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IRequestHandler<TRequest, TResponse> Link(
            IPipelineBehavior<TRequest, TResponse> behavior,
            IRequestHandler<TRequest, TResponse> next)
        {
            var factories = Volatile.Read(ref _factories);

            if (factories is not null
                && factories.TryGetValue(behavior.GetType(), out var factory))
            {
                return factory(behavior, next);
            }

            return new BehaviorHandlerAdapter<TRequest, TResponse>(behavior, next);
        }

        /// <summary>Test-only: the table is process-global, so suites need isolation between cases.</summary>
        internal static void ResetForTests() => Volatile.Write(ref _factories, null);
    }
}
