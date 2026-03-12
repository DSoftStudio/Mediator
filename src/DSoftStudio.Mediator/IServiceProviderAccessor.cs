// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// Provides access to the underlying <see cref="IServiceProvider"/> for
    /// compile-time interceptors that bypass virtual dispatch on the hot path.
    /// <para>
    /// Implemented by the default <c>Mediator</c>. Interceptor-generated code
    /// casts <see cref="Abstractions.ISender"/> to this interface to resolve services directly —
    /// eliminating the virtual call through <c>ISender.Send</c>.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IServiceProviderAccessor
    {
        /// <summary>
        /// The scoped <see cref="IServiceProvider"/> used for handler resolution.
        /// </summary>
        IServiceProvider ServiceProvider { get; }
    }
}
