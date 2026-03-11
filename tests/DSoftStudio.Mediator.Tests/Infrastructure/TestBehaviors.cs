// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;

namespace DSoftStudio.Mediator.Tests.Infrastructure;

// ── Request pipeline behaviors ────────────────────────────────────

/// <summary>
/// A behavior that records "before" and "after" entries in a shared log.
/// </summary>
public sealed class TrackingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    private readonly string _name;

    public TrackingBehavior(List<string> log, string name)
    {
        _log = log;
        _name = name;
    }

    public async ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        _log.Add($"{_name}:before");
        var result = await next.Handle(request, cancellationToken);
        _log.Add($"{_name}:after");
        return result;
    }
}

/// <summary>
/// A behavior that simply passes through — used to verify chaining works.
/// </summary>
public sealed class PassThroughBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public ValueTask<TResponse> Handle(
        TRequest request,
        IRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        return next.Handle(request, cancellationToken);
    }
}

// ── Stream pipeline behaviors ─────────────────────────────────────

public sealed class TrackingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly List<string> _log;
    private readonly string _name;

    public TrackingStreamBehavior(List<string> log, string name)
    {
        _log = log;
        _name = name;
    }

    public IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        _log.Add($"{_name}:enter");
        return next.Handle(request, cancellationToken);
    }
}
