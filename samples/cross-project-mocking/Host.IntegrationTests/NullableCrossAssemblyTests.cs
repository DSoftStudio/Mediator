// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

// ============================================================================
// Cross-Assembly Nullable Integration Tests
// ============================================================================
//
// This project mirrors the Host composition root:
//   • References DSoftStudio.Mediator + Generators
//   • References Host.Application (Abstractions-only — no generators there)
//
// The source generators discover Host.Application's handlers via
// ReferencedAssemblyScanner Phase 2 (type-based fallback) and generate:
//   1. Typed extensions:  sender.Send(new FindUserQuery(...))
//   2. DI registration:   services.RegisterMediatorHandlers()
//   3. Interceptors:      direct pipeline dispatch (bypasses virtual calls)
//
// These tests verify that nullable response types (e.g., IQuery<UserDto?>)
// survive the cross-assembly discovery path and produce correct generated
// code with no CS8631 warnings.
// ============================================================================

using DSoftStudio.Mediator;
using DSoftStudio.Mediator.Abstractions;
using Host.Application.Models;
using Host.Application.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Host.IntegrationTests;

/// <summary>
/// Integration tests that exercise nullable response types discovered from a
/// referenced assembly (Host.Application) via <c>ReferencedAssemblyScanner</c>.
/// A successful build of this project already proves CS8631 is resolved for
/// cross-assembly handlers; the runtime assertions verify the handlers
/// actually return <c>null</c> and non-null values through the real pipeline.
/// </summary>
public class NullableCrossAssemblyTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public NullableCrossAssemblyTests()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .RegisterMediatorHandlers()
            .PrecompilePipelines();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    // ── FindUserQuery : IQuery<UserDto?> — non-null path ─────────

    [Fact]
    public async Task Send_CrossAssembly_NullableRefType_ReturnsValue()
    {
        var result = await _mediator.Send(
            new FindUserQuery("alice"), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("alice");
    }

    // ── FindUserQuery : IQuery<UserDto?> — null path ─────────────

    [Fact]
    public async Task Send_CrossAssembly_NullableRefType_ReturnsNull()
    {
        var result = await _mediator.Send(
            new FindUserQuery("missing"), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── Existing non-nullable handlers still work ────────────────

    [Fact]
    public async Task Send_CrossAssembly_NonNullable_GetOrderQuery_ReturnsValue()
    {
        var result = await _mediator.Send(
            new GetOrderQuery(42), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldContain("42");
    }

    // ── ValidateMediatorHandlers — cross-assembly fail-fast ──────

    [Fact]
    public void ValidateMediatorHandlers_CrossAssembly_DoesNotThrow()
    {
        // If the generator failed to emit the correct nullable type for
        // FindUserQuery's IQueryHandler<FindUserQuery, UserDto?>, the
        // validation would either not compile or throw at runtime.
        _provider.ValidateMediatorHandlers();
    }
}
