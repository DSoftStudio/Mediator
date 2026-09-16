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

    /// <summary>
    /// The constructor normalizes null, but a struct has a form no constructor guards: <c>default</c>,
    /// which every uninitialised field of a containing struct also takes. The incremental pipeline
    /// calls Equals and GetHashCode on everything it caches, so an unguarded default does not fail
    /// where it was written — it fails as <c>CS8785: Generator failed to generate source ...
    /// NullReferenceException</c>, naming no file, no line and no member. These pin the behaviour
    /// that keeps that from happening.
    /// </summary>
    [Fact]
    public void Default_Instance_Behaves_As_Empty()
    {
        var uninitialised = default(EquatableArray<string>);

        uninitialised.Length.ShouldBe(0);
        uninitialised.GetHashCode().ShouldBe(EquatableArray<string>.Empty.GetHashCode());
        uninitialised.Equals(EquatableArray<string>.Empty).ShouldBeTrue();
        EquatableArray<string>.Empty.Equals(uninitialised).ShouldBeTrue();
        uninitialised.ToList().ShouldBeEmpty();
    }

    [Fact]
    public void Default_Instance_Is_Not_Equal_To_A_Populated_One()
    {
        var uninitialised = default(EquatableArray<string>);
        var populated = new EquatableArray<string>(["a"]);

        uninitialised.Equals(populated).ShouldBeFalse();
        populated.Equals(uninitialised).ShouldBeFalse();
    }

    /// <summary>
    /// <c>IEquatable&lt;T&gt;</c> is satisfied by reference types, so an ELEMENT can be null even
    /// when the array is not. Comparing those with <c>item.Equals(...)</c> throws.
    /// </summary>
    [Fact]
    public void Null_Elements_Compare_And_Hash_Without_Throwing()
    {
        var withNull = new EquatableArray<string>([null!, "b"]);
        var sameAgain = new EquatableArray<string>([null!, "b"]);
        var different = new EquatableArray<string>(["a", "b"]);

        withNull.Equals(sameAgain).ShouldBeTrue();
        withNull.GetHashCode().ShouldBe(sameAgain.GetHashCode());
        withNull.Equals(different).ShouldBeFalse();
    }
}
