// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Generators;

namespace DSoftStudio.Mediator.Tests.Generators;

/// <summary>
/// Direct unit tests for <see cref="BehaviorTypeInfo"/> — the value record the generators use to carry a
/// discovered open-generic pipeline component (behavior / processor / exception handler / stream behavior)
/// through the incremental pipeline. It was at 20% branch coverage.
/// </summary>
public class BehaviorTypeInfoTests
{
    [Fact]
    public void Properties_Reflect_Constructor()
    {
        var b = new BehaviorTypeInfo(PipelineInterfaceKind.Behavior, "global::App.Logging<,>", "global::App.Logging");

        b.Kind.ShouldBe(PipelineInterfaceKind.Behavior);
        b.OpenTypeName.ShouldBe("global::App.Logging<,>");
        b.BaseTypeName.ShouldBe("global::App.Logging");
    }

    [Fact]
    public void Equality_Is_Structural_Across_All_Fields()
    {
        var a = new BehaviorTypeInfo(PipelineInterfaceKind.StreamBehavior, "Open", "Base");
        var same = new BehaviorTypeInfo(PipelineInterfaceKind.StreamBehavior, "Open", "Base");
        var diffKind = new BehaviorTypeInfo(PipelineInterfaceKind.Behavior, "Open", "Base");
        var diffOpen = new BehaviorTypeInfo(PipelineInterfaceKind.StreamBehavior, "Other", "Base");
        var diffBase = new BehaviorTypeInfo(PipelineInterfaceKind.StreamBehavior, "Open", "Other");

        a.Equals(same).ShouldBeTrue();
        a.Equals(diffKind).ShouldBeFalse();   // Kind differs
        a.Equals(diffOpen).ShouldBeFalse();   // OpenTypeName differs
        a.Equals(diffBase).ShouldBeFalse();   // BaseTypeName differs
        a.Equals((object)same).ShouldBeTrue();
        a.Equals((object)"not a behavior").ShouldBeFalse();
    }

    [Fact]
    public void Equal_Values_Share_HashCode()
    {
        var a = new BehaviorTypeInfo(PipelineInterfaceKind.ExceptionHandler, "O", "B");
        var same = new BehaviorTypeInfo(PipelineInterfaceKind.ExceptionHandler, "O", "B");

        a.GetHashCode().ShouldBe(same.GetHashCode());
    }

    [Fact]
    public void HashCode_Is_Null_Safe()
    {
        // GetHashCode uses `OpenTypeName?.GetHashCode() ?? 0` — exercise the null branch so it never throws.
        var b = new BehaviorTypeInfo(PipelineInterfaceKind.PostProcessor, null!, null!);

        _ = b.GetHashCode();
        b.OpenTypeName.ShouldBeNull();
        b.BaseTypeName.ShouldBeNull();
    }
}
