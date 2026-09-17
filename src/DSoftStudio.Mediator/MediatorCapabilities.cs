// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// What this build of the core guarantees, for tooling that generates code against it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A source generator in another package cannot ask the running library what it does — it sees a
    /// compilation, not a process. Without something explicit to read, the only option is to infer
    /// capability from a proxy: "type X exists, therefore behaviour Y is present". That works exactly
    /// as long as the two happen to have shipped together.
    /// </para>
    /// <para>
    /// This class exists because a downstream generator was doing precisely that. It read the presence
    /// of <see cref="Abstractions.IMediatorDispatchObserver"/> as evidence that the core folds handler
    /// lifetime into the chain's, because that type and that fix landed in the same commit. True today,
    /// and by coincidence: moving or renaming the type would have made the generator register
    /// Singleton components against a core that does not fold, and the consumer's application would
    /// have failed in <c>BuildServiceProvider</c> — at startup, not at build, in a codebase whose
    /// authors own neither package.
    /// </para>
    /// <para>
    /// Read these from a generator with <c>GetTypesByMetadataName</c> and
    /// <c>IFieldSymbol.ConstantValue</c>. They are integers rather than booleans so a capability can
    /// advance without a new name: a consumer checks <c>&gt;=</c> the revision it needs.
    /// </para>
    /// <para>
    /// <b>Adding one is a promise.</b> Each constant here is covered by a test that exercises the
    /// behaviour it claims, not the constant. A capability marker nobody verifies is worse than none —
    /// it converts an inference that might be wrong into a statement that is trusted.
    /// </para>
    /// </summary>
    public static class MediatorCapabilities
    {
        /// <summary>
        /// Revision 1: the lifetime of a handler constrains the lifetime of the pipeline chain that
        /// consumes it. A Transient handler yields a Transient chain, rebuilt per dispatch; a Scoped
        /// handler or dispatch observer stops the chain being a Singleton.
        /// </summary>
        /// <remarks>
        /// Tooling that registers its own pipeline components needs this to decide their lifetime. Get
        /// it wrong against a core that does not fold and the container refuses to build under
        /// <c>ValidateScopes</c>, or — worse — builds and captures a narrower dependency in a
        /// longer-lived chain.
        /// </remarks>
        public const int ChainLifetimeFold = 1;
    }
}
