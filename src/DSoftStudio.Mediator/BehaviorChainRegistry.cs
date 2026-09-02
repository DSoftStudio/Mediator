// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.ComponentModel;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Per-(request, response) factory for a fully specialized behavior chain, where EVERY link stores
    /// both its behavior and the next link in fields typed to their concrete classes.
    /// <para>
    /// <b>Why a whole-chain factory and not a per-link one.</b> A link can only devirtualize the
    /// <c>next.Handle</c> call that happens inside the user's behavior body if the JIT can see the
    /// concrete type of <c>next</c> at the inlined call site — which means the link's <c>next</c> field
    /// must be typed to the next link's concrete class, which means the whole ordered chain has to be
    /// known when the code is generated. Measured on .NET 11 preview 7, three distinct pass-through
    /// behaviors, per-link cost:
    /// </para>
    /// <list type="bullet">
    /// <item><description>interface behavior + interface next (today): <b>3.08 ns</b></description></item>
    /// <item><description>concrete behavior + interface next (<see cref="BehaviorLinkRegistry{TRequest, TResponse}"/>): <b>1.24 ns</b></description></item>
    /// <item><description>concrete behavior + concrete next (this): <b>0.10 ns</b></description></item>
    /// </list>
    /// <para>
    /// <b>The plan is a hypothesis; the resolved instances are the truth.</b> The generator predicts the
    /// chain from registration sites, which cannot see conditional registration, factory lambdas, or
    /// registrations inside a referenced assembly's method body. So the generated factory re-checks, at
    /// chain construction: the array length, and the EXACT runtime type of every behavior instance in
    /// order. Any mismatch returns <see langword="null"/> and the caller falls back to the per-link tier
    /// and then to the interface-typed adapter. A wrong prediction therefore costs performance, never
    /// correctness — the same fail-open discipline as the ADR-0065 concrete handler caches.
    /// </para>
    /// <para>
    /// Exactness is load-bearing: a decorator wrapping a behavior, or a subclass hiding <c>Handle</c>
    /// with <c>new</c>, must NOT be routed through a link typed to the base class, which would bind the
    /// base implementation statically and silently run the wrong code.
    /// </para>
    /// <para>
    /// <see cref="TryBuild"/> runs from the <see cref="PipelineChainHandler{TRequest, TResponse}"/>
    /// constructor — once per DI scope, never on the dispatch path.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class BehaviorChainRegistry<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Builds the fully specialized chain, or returns <see langword="null"/> when the resolved
        /// behaviors do not match the sequence the generator predicted.
        /// </summary>
        public delegate IRequestHandler<TRequest, TResponse>? ChainFactory(
            IPipelineBehavior<TRequest, TResponse>[] behaviors,
            IRequestHandler<TRequest, TResponse> handler);

        // One candidate per composition root, accumulated during registration and read from scope
        // construction. A single file can build several IServiceCollection instances with DIFFERENT
        // chains for the same pair, so a single slot would let the last registration silently discard
        // the others. Published as a whole array; never mutated after publication.
        private static ChainFactory[]? _factories;

        /// <summary>
        /// Registers one candidate chain factory. Called from generated registration code, once per
        /// predicted composition root.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Register(ChainFactory factory)
        {
            var current = Volatile.Read(ref _factories);

            ChainFactory[] next;
            if (current is null)
            {
                next = [factory];
            }
            else
            {
                // Idempotent: PrecompilePipelines may run more than once over the same collection.
                foreach (var existing in current)
                {
                    if (existing == factory)
                        return;
                }

                next = new ChainFactory[current.Length + 1];
                Array.Copy(current, next, current.Length);
                next[current.Length] = factory;
            }

            Volatile.Write(ref _factories, next);
        }

        /// <summary>
        /// Returns the first candidate whose verification passes against the resolved instances, or
        /// <see langword="null"/> when none matches — meaning the caller should build link by link.
        /// Runs once per DI scope, so trying a handful of candidates is free.
        /// </summary>
        internal static IRequestHandler<TRequest, TResponse>? TryBuild(
            IPipelineBehavior<TRequest, TResponse>[] behaviors,
            IRequestHandler<TRequest, TResponse> handler)
        {
            var factories = Volatile.Read(ref _factories);
            if (factories is null)
                return null;

            foreach (var factory in factories)
            {
                var chain = factory(behaviors, handler);
                if (chain is not null)
                    return chain;
            }

            return null;
        }

        /// <summary>Test-only: the table is process-global, so suites need isolation between cases.</summary>
        internal static void ResetForTests() => Volatile.Write(ref _factories, null);
    }
}
