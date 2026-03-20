// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.Tests;

public class UnitTests
{
    [Fact]
    public void Equals_Unit_ReturnsTrue()
    {
        Unit.Value.Equals(Unit.Value).ShouldBeTrue();
    }

    [Fact]
    public void Equals_Object_ReturnsTrue_WhenUnit()
    {
        Unit.Value.Equals((object)Unit.Value).ShouldBeTrue();
    }

    [Fact]
    public void Equals_Object_ReturnsFalse_WhenNotUnit()
    {
        Unit.Value.Equals("not a unit").ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_ReturnsZero()
    {
        Unit.Value.GetHashCode().ShouldBe(0);
    }

    [Fact]
    public void ToString_ReturnsParentheses()
    {
        Unit.Value.ToString().ShouldBe("()");
    }
}
