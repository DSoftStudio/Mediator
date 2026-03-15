// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSoftStudio.Mediator
{
    /// <summary>
    /// AOT-safe static dispatch table for <see cref="ISender.Send(object, CancellationToken)"/>.
    /// <para>
    /// Populated at startup by the generated <c>MediatorRegistry.RegisterPipelineChains()</c>.
    /// Each request type gets a compile-time generated delegate — no
    /// <c>MakeGenericType</c>, no <c>Expression.Compile</c>, no reflection.
    /// After all registrations complete, <see cref="Freeze"/> converts the table to
    /// a <see cref="FrozenDictionary{TKey, TValue}"/> for optimal concurrent read performance.
    /// </para>
    /// <para><b>Infrastructure type — not intended for direct use by application code.</b></para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class RequestObjectDispatch
    {
        /// <summary>
        /// Dispatch delegate signature for runtime-typed request sending.
        /// Returns the handler response boxed as <see cref="object"/>.
        /// </summary>
        public delegate ValueTask<object?> DispatchDelegate(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);

        // Mutable during registration; frozen snapshot created by Freeze().
        // ConcurrentDictionary ensures thread-safety when multiple hosts or test
        // runners call Register() in parallel before Freeze() is invoked.
        private static readonly ConcurrentDictionary<Type, DispatchDelegate> _mutableDispatchers = new();
        private static FrozenDictionary<Type, DispatchDelegate> _dispatchers = FrozenDictionary<Type, DispatchDelegate>.Empty;

        /// <summary>
        /// Registers a compile-time generated dispatch delegate for a request type.
        /// Called once at startup by the generated <c>MediatorRegistry</c>.
        /// Idempotent — safe to call multiple times (e.g. in test isolation).
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Register<TRequest, TResponse>(DispatchDelegate dispatcher)
            where TRequest : IRequest<TResponse>
        {
            _mutableDispatchers[typeof(TRequest)] = dispatcher;
        }

        /// <summary>
        /// Creates a <see cref="FrozenDictionary{TKey, TValue}"/> snapshot from all
        /// registered dispatchers for optimal read performance.
        /// Called at the end of <c>PrecompilePipelines()</c>.
        /// Idempotent — safe to call multiple times.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Freeze()
        {
            _dispatchers = _mutableDispatchers.ToFrozenDictionary();
        }

        /// <summary>
        /// Dispatches a request using the compile-time generated delegate.
        /// Falls back to a descriptive error if the request type wasn't registered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<object?> Dispatch(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            if (_dispatchers.TryGetValue(request.GetType(), out var dispatcher))
                return dispatcher(request, serviceProvider, cancellationToken);

            throw new InvalidOperationException(
                $"No request handler registered for {request.GetType().Name}. " +
                "Ensure PrecompilePipelines() is called during service configuration.");
        }
    }
}
