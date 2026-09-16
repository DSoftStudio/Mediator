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
    /// <para>
    /// Every member goes through <see cref="Items"/> rather than the field, because a struct has a
    /// parameterless form no constructor can guard: <c>default(EquatableArray&lt;T&gt;)</c>, or any
    /// containing struct with this as an uninitialised field, carries a NULL array. That value does
    /// not stay quiet — the incremental pipeline calls Equals and GetHashCode on everything it
    /// caches, so it surfaces as <c>CSC : warning CS8785: Generator failed to generate source ...
    /// NullReferenceException</c>, which names no file, no line and no member. It cost real time to
    /// track down once; guarding here means it cannot happen again.
    /// </para>
    /// </summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

        private readonly T[] _array;

        public EquatableArray(T[] array) => _array = array ?? Array.Empty<T>();

        /// <summary>The backing array, never null — see the note on the type.</summary>
        private T[] Items => _array ?? Array.Empty<T>();

        public int Length => Items.Length;

        public T this[int index] => Items[index];

        public bool Equals(EquatableArray<T> other)
        {
            var mine = Items;
            var theirs = other.Items;

            if (mine.Length != theirs.Length)
                return false;

            for (int i = 0; i < mine.Length; i++)
            {
                // Default comparer, not item.Equals: T is only constrained to IEquatable<T>, which
                // a reference type satisfies while still allowing a null ELEMENT.
                if (!EqualityComparer<T>.Default.Equals(mine[i], theirs[i]))
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
                foreach (var item in Items)
                    hash = hash * 31 + (item?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public IEnumerator<T> GetEnumerator() =>
            ((IEnumerable<T>)Items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
