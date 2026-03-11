// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using DSoftStudio.Mediator.Abstractions;

namespace DSoft.Sample.Application.Queries;

public record HelloWorldQuery : IQuery<string>;

public sealed class HelloWorldQueryHandler : IQueryHandler<HelloWorldQuery, string>
{
    public ValueTask<string> Handle(HelloWorldQuery request, CancellationToken cancellationToken)
        => new("Hello World from DSoft Mediator!");
}
