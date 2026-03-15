// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using FluentValidation.Results;

namespace DSoftStudio.Mediator.FluentValidation.Tests;

public class MediatorValidationExceptionTests
{
    [Fact]
    public void ErrorsByProperty_groups_failures_correctly()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required."),
            new("Email", "Email is required."),
            new("Email", "Email must be valid."),
        };

        var ex = new MediatorValidationException(failures);

        ex.ErrorsByProperty.ShouldContainKey("Name");
        ex.ErrorsByProperty["Name"].Length.ShouldBe(1);

        ex.ErrorsByProperty.ShouldContainKey("Email");
        ex.ErrorsByProperty["Email"].Length.ShouldBe(2);
    }

    [Fact]
    public void Failures_property_contains_all_failures()
    {
        var failures = new List<ValidationFailure>
        {
            new("A", "err1"),
            new("B", "err2"),
        };

        var ex = new MediatorValidationException(failures);

        ex.Failures.Count.ShouldBe(2);
    }

    [Fact]
    public void Message_single_failure()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required."),
        };

        var ex = new MediatorValidationException(failures);

        ex.Message.ShouldContain("Name is required.");
    }

    [Fact]
    public void Message_multiple_failures()
    {
        var failures = new List<ValidationFailure>
        {
            new("A", "err1"),
            new("B", "err2"),
            new("C", "err3"),
        };

        var ex = new MediatorValidationException(failures);

        ex.Message.ShouldContain("3 errors");
    }

    [Fact]
    public void Message_zero_failures()
    {
        var ex = new MediatorValidationException([]);

        ex.Message.ShouldBe("Validation failed.");
    }
}
