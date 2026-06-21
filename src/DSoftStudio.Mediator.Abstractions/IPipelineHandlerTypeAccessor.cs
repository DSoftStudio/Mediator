// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Exposes the CONCRETE handler type at the tail of the pipeline chain.
    /// <para>
    /// A pipeline behavior is open-generic and may be shared by many handlers, so a behavior cannot know
    /// — from its own type — which handler THIS request resolves to. The correct handler is only knowable by
    /// walking the chain the behavior was handed as <c>next</c> down to the terminal handler. The internal
    /// chain adapters implement this so an outermost behavior (tracing, diagnostics) can read the concrete
    /// handler type without resolving or instantiating anything — the chain is already built.
    /// </para>
    /// <para>
    /// Implemented by the request and stream behavior-chain adapters; the terminal handler does not implement
    /// it, so a consumer resolves the type as <c>next is IPipelineHandlerTypeAccessor a ? a.HandlerType : next.GetType()</c>.
    /// </para>
    /// </summary>
    public interface IPipelineHandlerTypeAccessor
    {
        /// <summary>
        /// The concrete <c>IRequestHandler</c> / <c>IStreamRequestHandler</c> implementation type at the end
        /// of this chain (resolved by walking <c>next</c> to the terminal handler).
        /// </summary>
        Type HandlerType { get; }
    }
}
