// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace DSoftStudio.Mediator.Abstractions
{
    /// <summary>
    /// Represents a void return type for requests that produce no meaningful result.
    /// enabling uniform pipeline execution for all request types.
    /// </summary>
    public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
    {
        public static readonly Unit Value = new Unit();

        public static readonly Task<Unit> Task =
            System.Threading.Tasks.Task.FromResult(Value);
      
        public static readonly ValueTask<Unit> ValueTask = new ValueTask<Unit>(Value);

        public int CompareTo(Unit other) => 0;

        public int CompareTo(object? obj) => obj is Unit ? 0 : -1;

        public bool Equals(Unit other) => true;

        public override bool Equals(object? obj) => obj is Unit;
        public override int GetHashCode() => 0;

        public override string ToString() => "()";

        public static bool operator ==(Unit left, Unit right) => true;

        public static bool operator !=(Unit left, Unit right) => false;
    }
}

