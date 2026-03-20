// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Represents a void return type for requests that produce no meaningful result,
    /// enabling uniform pipeline execution for all request types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Unit</c> intentionally does <strong>not</strong> implement
    /// <see cref="IComparable{T}"/> or <see cref="IComparable"/>, and exposes
    /// <see cref="ValueTask"/> as a property rather than a <c>static readonly</c> field.
    /// </para>
    /// <para>
    /// The .NET 10 SDK compiler emits metadata for <c>ValueTask&lt;T&gt;</c> static fields
    /// on structs that is incompatible with the .NET 8 runtime, producing a
    /// <see cref="TypeLoadException"/> at startup. Since <c>ValueTask&lt;T&gt;</c> is itself
    /// a struct, constructing a new instance on each access is zero-allocation and free.
    /// </para>
    /// </remarks>
    public readonly struct Unit : IEquatable<Unit>
    {
        /// <summary>The singleton <see cref="Unit"/> value.</summary>
        public static readonly Unit Value = default;

        /// <summary>
        /// A pre-completed <see cref="System.Threading.Tasks.Task{TResult}"/> containing <see cref="Value"/>.
        /// </summary>
        public static readonly Task<Unit> Task =
            System.Threading.Tasks.Task.FromResult(Value);

        /// <summary>
        /// A pre-completed <see cref="System.Threading.Tasks.ValueTask{TResult}"/> containing <see cref="Value"/>.
        /// Exposed as a property (not a field) to avoid a <see cref="TypeLoadException"/> when
        /// the assembly is compiled with the .NET 10+ SDK but loaded by the .NET 8 runtime.
        /// </summary>
        public static ValueTask<Unit> ValueTask => new ValueTask<Unit>(Value);

        /// <inheritdoc />
        public bool Equals(Unit other) => true;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Unit;

        /// <inheritdoc />
        public override int GetHashCode() => 0;

        /// <inheritdoc />
        public override string ToString() => "()";

        public static bool operator ==(Unit left, Unit right) => true;

        public static bool operator !=(Unit left, Unit right) => false;
    }
}

