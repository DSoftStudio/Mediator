// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// The dependency-injection lifetime a mediator handler is registered with. Mirrors the three
    /// <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c> values without coupling the
    /// abstractions package to that dependency.
    /// </summary>
    public enum HandlerLifetime
    {
        /// <summary>A new instance per resolution.</summary>
        Transient,

        /// <summary>One instance per DI scope (e.g. per web request).</summary>
        Scoped,

        /// <summary>A single shared instance for the whole application lifetime.</summary>
        Singleton,
    }

    /// <summary>
    /// Pins the DI lifetime the mediator registers this handler with, overriding the automatic
    /// dependency-driven detection.
    /// <para>
    /// By default the mediator picks the lifetime that matches the handler's constructor dependencies:
    /// <see cref="HandlerLifetime.Singleton"/> when every dependency is itself a singleton (cached,
    /// zero-allocation per request), <see cref="HandlerLifetime.Scoped"/> when any dependency is scoped
    /// (cached per scope), <see cref="HandlerLifetime.Transient"/> otherwise. Apply this attribute when
    /// the handler must use a specific lifetime regardless - for example
    /// <see cref="HandlerLifetime.Transient"/> when it must be a fresh instance per call because it (or
    /// a dependency) carries per-call state.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HandlerLifetimeAttribute : Attribute
    {
        /// <summary>Initializes the attribute with the lifetime to register the handler with.</summary>
        /// <param name="lifetime">The lifetime to pin.</param>
        public HandlerLifetimeAttribute(HandlerLifetime lifetime) => Lifetime = lifetime;

        /// <summary>The pinned lifetime.</summary>
        public HandlerLifetime Lifetime { get; }
    }
}
