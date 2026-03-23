// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DSoftStudio.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.Tests.Nullable;

// ── Types under test ──────────────────────────────────────────────

// ─── Nullable reference type (class): UserDto? ───────────────────

/// <summary>Simple reference type used as a nullable response.</summary>
public class UserDto
{
    public string Name { get; init; } = string.Empty;
}

public record GetNullableUser(string UserId) : IRequest<UserDto?>;

public sealed class GetNullableUserHandler : IRequestHandler<GetNullableUser, UserDto?>
{
    public ValueTask<UserDto?> Handle(GetNullableUser request, CancellationToken cancellationToken)
    {
        if (request.UserId == "missing")
            return new ValueTask<UserDto?>((UserDto?)null);

        return new ValueTask<UserDto?>(new UserDto { Name = request.UserId });
    }
}

// ─── Nullable reference type (string): string? ───────────────────

public record GetNullableString(bool ReturnNull) : IRequest<string?>;

public sealed class GetNullableStringHandler : IRequestHandler<GetNullableString, string?>
{
    public ValueTask<string?> Handle(GetNullableString request, CancellationToken cancellationToken)
        => new(request.ReturnNull ? null : "hello");
}

// ─── Nullable value type: int? (Nullable<int>) ──────────────────

public record GetNullableInt(bool ReturnNull) : IRequest<int?>;

public sealed class GetNullableIntHandler : IRequestHandler<GetNullableInt, int?>
{
    public ValueTask<int?> Handle(GetNullableInt request, CancellationToken cancellationToken)
        => new(request.ReturnNull ? null : 42);
}

// ─── Nullable stream response: IStreamRequest<string?> ───────────

public record GetNullableStream(int Count) : IStreamRequest<string?>;

public sealed class GetNullableStreamHandler : IStreamRequestHandler<GetNullableStream, string?>
{
    public async IAsyncEnumerable<string?> Handle(
        GetNullableStream request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
        {
            yield return i % 2 == 0 ? $"item-{i}" : null;
        }
    }
}

// ─── Self-handling request with nullable response ────────────────

public record SelfHandledNullableQuery(string UserId) : IQuery<UserDto?>
{
    internal static UserDto? Execute(SelfHandledNullableQuery query)
    {
        return query.UserId == "missing" ? null : new UserDto { Name = query.UserId };
    }
}

// ─── Nested generic nullable: List<UserDto?>? ────────────────────

public record GetNullableList(bool ReturnNull) : IRequest<List<UserDto?>?>;

public sealed class GetNullableListHandler : IRequestHandler<GetNullableList, List<UserDto?>?>
{
    public ValueTask<List<UserDto?>?> Handle(GetNullableList request, CancellationToken cancellationToken)
    {
        if (request.ReturnNull)
            return new ValueTask<List<UserDto?>?>((List<UserDto?>?)null);

        return new ValueTask<List<UserDto?>?>(new List<UserDto?>
        {
            new() { Name = "alice" },
            null,
            new() { Name = "bob" }
        });
    }
}

// ─── Dedicated types for pipeline behavior tests ─────────────────
// Separate request types to avoid static pipeline state conflicts with
// the no-behavior tests above.

public record GetNullableUserWithPipeline(string UserId) : IRequest<UserDto?>;

public sealed class GetNullableUserWithPipelineHandler : IRequestHandler<GetNullableUserWithPipeline, UserDto?>
{
    public ValueTask<UserDto?> Handle(GetNullableUserWithPipeline request, CancellationToken cancellationToken)
    {
        return request.UserId == "missing"
            ? new ValueTask<UserDto?>((UserDto?)null)
            : new ValueTask<UserDto?>(new UserDto { Name = request.UserId });
    }
}

public record GetNullableStreamWithPipeline(int Count) : IStreamRequest<string?>;

public sealed class GetNullableStreamWithPipelineHandler : IStreamRequestHandler<GetNullableStreamWithPipeline, string?>
{
    public async IAsyncEnumerable<string?> Handle(
        GetNullableStreamWithPipeline request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
            yield return i % 2 == 0 ? $"item-{i}" : null;
    }
}

// ── Tests ─────────────────────────────────────────────────────────

/// <summary>
/// Integration tests that exercise nullable response types across all generator code paths.
/// The test project compiles with <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c>,
/// so the source generators must emit the <c>?</c> annotation in generated code.
/// A successful build of this file already proves CS8631 is resolved; the runtime
/// assertions verify the handlers actually return <c>null</c> and non-null values.
///
/// Covers six categories:
/// <list type="bullet">
///   <item><c>UserDto?</c> — nullable reference type (class)</item>
///   <item><c>string?</c> — nullable reference type (built-in)</item>
///   <item><c>int?</c> — nullable value type (<c>Nullable&lt;int&gt;</c>)</item>
///   <item><c>IStreamRequest&lt;string?&gt;</c> — nullable stream response</item>
///   <item><c>IQuery&lt;UserDto?&gt;</c> — self-handling request with nullable response</item>
///   <item><c>List&lt;UserDto?&gt;?</c> — nested generic with nullable elements and nullable container</item>
/// </list>
/// </summary>
public class NullableResponseTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public NullableResponseTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines()
            .PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    // ── UserDto? (nullable reference type — class) ────────────────

    [Fact]
    public async Task Send_NullableRefType_ReturnsValue()
    {
        var result = await _mediator.Send(new GetNullableUser("alice"), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("alice");
    }

    [Fact]
    public async Task Send_NullableRefType_ReturnsNull()
    {
        var result = await _mediator.Send(new GetNullableUser("missing"), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── string? (nullable reference type — built-in) ─────────────

    [Fact]
    public async Task Send_NullableString_ReturnsValue()
    {
        var result = await _mediator.Send(new GetNullableString(false), TestContext.Current.CancellationToken);

        result.ShouldBe("hello");
    }

    [Fact]
    public async Task Send_NullableString_ReturnsNull()
    {
        var result = await _mediator.Send(new GetNullableString(true), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── int? (nullable value type — Nullable<int>) ───────────────

    [Fact]
    public async Task Send_NullableValueType_ReturnsValue()
    {
        var result = await _mediator.Send(new GetNullableInt(false), TestContext.Current.CancellationToken);

        result.ShouldBe(42);
    }

    [Fact]
    public async Task Send_NullableValueType_ReturnsNull()
    {
        var result = await _mediator.Send(new GetNullableInt(true), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── IStreamRequest<string?> (nullable stream response) ───────

    [Fact]
    public async Task CreateStream_NullableResponse_YieldsValuesAndNulls()
    {
        var items = new List<string?>();

        await foreach (var item in _mediator.CreateStream(new GetNullableStream(4), TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        items.Count.ShouldBe(4);
        items[0].ShouldBe("item-0");
        items[1].ShouldBeNull();
        items[2].ShouldBe("item-2");
        items[3].ShouldBeNull();
    }

    // ── Self-handling request with nullable response ─────────────

    [Fact]
    public async Task Send_SelfHandler_NullableResponse_ReturnsValue()
    {
        var result = await _mediator.Send<SelfHandledNullableQuery, UserDto?>(
            new SelfHandledNullableQuery("alice"), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("alice");
    }

    [Fact]
    public async Task Send_SelfHandler_NullableResponse_ReturnsNull()
    {
        var result = await _mediator.Send<SelfHandledNullableQuery, UserDto?>(
            new SelfHandledNullableQuery("missing"), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── Nested generic: List<UserDto?>? ──────────────────────────

    [Fact]
    public async Task Send_NestedGenericNullable_ReturnsListWithNullElements()
    {
        var result = await _mediator.Send(new GetNullableList(false), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result[0].ShouldNotBeNull();
        result[0]!.Name.ShouldBe("alice");
        result[1].ShouldBeNull();
        result[2].ShouldNotBeNull();
        result[2]!.Name.ShouldBe("bob");
    }

    [Fact]
    public async Task Send_NestedGenericNullable_ReturnsNull()
    {
        var result = await _mediator.Send(new GetNullableList(true), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }
}

/// <summary>
/// Tests that exercise nullable response types through the <b>pipeline chain</b> path
/// (<c>PipelineChainHandler&lt;TRequest, TResponse?&gt;</c>).
/// The base <see cref="NullableResponseTests"/> runs without behaviors (direct handler dispatch).
/// This class registers <c>IPipelineBehavior</c> and <c>IStreamPipelineBehavior</c>
/// to force the chain/cache code paths that use the nullable type arguments.
/// </summary>
public class NullablePipelineBehaviorTests : IDisposable
{
    private readonly List<string> _log = [];
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public NullablePipelineBehaviorTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers();
        services.AddSingleton(_log);

        // Register pipeline behavior for nullable reference-type response.
        // This forces PipelineChainHandler<GetNullableUserWithPipeline, UserDto?> through DI.
        services.AddTransient<IPipelineBehavior<GetNullableUserWithPipeline, UserDto?>>(sp =>
            new Infrastructure.TrackingBehavior<GetNullableUserWithPipeline, UserDto?>(
                sp.GetRequiredService<List<string>>(), "B1"));

        // Register stream pipeline behavior for nullable stream response.
        // This forces StreamPipelineChainHandler<GetNullableStreamWithPipeline, string?>.
        services.AddTransient<IStreamPipelineBehavior<GetNullableStreamWithPipeline, string?>>(sp =>
            new Infrastructure.TrackingStreamBehavior<GetNullableStreamWithPipeline, string?>(
                sp.GetRequiredService<List<string>>(), "SB1"));

        services.PrecompilePipelines();
        services.PrecompileStreams();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    // ── Request pipeline with nullable response ──────────────────

    [Fact]
    public async Task Send_WithBehavior_NullableResponse_ReturnsValue()
    {
        var result = await _mediator.Send(
            new GetNullableUserWithPipeline("alice"), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("alice");
        _log.ShouldContain("B1:before");
        _log.ShouldContain("B1:after");
    }

    [Fact]
    public async Task Send_WithBehavior_NullableResponse_ReturnsNull()
    {
        var result = await _mediator.Send(
            new GetNullableUserWithPipeline("missing"), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        _log.ShouldContain("B1:before");
        _log.ShouldContain("B1:after");
    }

    // ── Stream pipeline with nullable response ───────────────────

    [Fact]
    public async Task CreateStream_WithBehavior_NullableResponse_YieldsValuesAndNulls()
    {
        var items = new List<string?>();

        await foreach (var item in _mediator.CreateStream(
            new GetNullableStreamWithPipeline(3), TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        items.Count.ShouldBe(3);
        items[0].ShouldBe("item-0");
        items[1].ShouldBeNull();
        items[2].ShouldBe("item-2");
        _log.ShouldContain("SB1:enter");
    }
}
