// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Direct unit tests for <see cref="EquatableArray{T}"/> — the value-equality wrapper every generator uses so
/// the incremental pipeline can compare collected results structurally. It was the least-covered generator type.
/// </summary>
public class EquatableArrayTests
{
    [Fact]
    public void Empty_And_Null_Constructor_Are_Length_Zero()
    {
        EquatableArray<int>.Empty.Length.ShouldBe(0);
        new EquatableArray<int>(null!).Length.ShouldBe(0); // null is normalized to Array.Empty
    }

    [Fact]
    public void Indexer_And_Length_Reflect_The_Backing_Array()
    {
        var a = new EquatableArray<int>(new[] { 10, 20, 30 });

        a.Length.ShouldBe(3);
        a[0].ShouldBe(10);
        a[2].ShouldBe(30);
    }

    [Fact]
    public void Equals_Is_Structural_ElementWise()
    {
        var a = new EquatableArray<int>(new[] { 1, 2, 3 });
        var same = new EquatableArray<int>(new[] { 1, 2, 3 });
        var diffElement = new EquatableArray<int>(new[] { 1, 2, 9 });
        var diffLength = new EquatableArray<int>(new[] { 1, 2 });

        a.Equals(same).ShouldBeTrue();
        a.Equals(diffElement).ShouldBeFalse();      // same length, different element
        a.Equals(diffLength).ShouldBeFalse();       // different length (early out)
        a.Equals((object)same).ShouldBeTrue();      // object overload, matching type
        a.Equals((object)"not an array").ShouldBeFalse(); // object overload, wrong type
    }

    [Fact]
    public void Equal_Arrays_Share_HashCode()
    {
        var a = new EquatableArray<string>(new[] { "x", "y" });
        var same = new EquatableArray<string>(new[] { "x", "y" });

        a.GetHashCode().ShouldBe(same.GetHashCode());
    }

    [Fact]
    public void Enumerates_All_Elements_Generic_And_NonGeneric()
    {
        var a = new EquatableArray<int>(new[] { 5, 6, 7 });

        a.ToList().ShouldBe(new[] { 5, 6, 7 }); // IEnumerable<T>.GetEnumerator

        var e = ((System.Collections.IEnumerable)a).GetEnumerator(); // explicit non-generic GetEnumerator
        e.MoveNext().ShouldBeTrue();
        e.Current.ShouldBe(5);
    }
}
