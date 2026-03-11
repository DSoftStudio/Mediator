// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace DSoftStudio.Mediator.Generators
{
    /// <summary>
    /// Array wrapper with structural equality — required for correct caching
    /// in incremental source generator pipelines.
    /// </summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

        private readonly T[] _array;

        public EquatableArray(T[] array) => _array = array ?? Array.Empty<T>();

        public int Length => _array.Length;

        public T this[int index] => _array[index];

        public bool Equals(EquatableArray<T> other)
        {
            if (_array.Length != other._array.Length)
                return false;

            for (int i = 0; i < _array.Length; i++)
            {
                if (!_array[i].Equals(other._array[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) =>
            obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var item in _array)
                    hash = hash * 31 + item.GetHashCode();
                return hash;
            }
        }

        public IEnumerator<T> GetEnumerator() =>
            ((IEnumerable<T>)_array).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
