// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace DSoftStudio.Mediator.OpenTelemetry.Tests.Fixtures;

/// <summary>
/// Builds a <see cref="MediatorMetrics"/> backed by a real DI <see cref="IMeterFactory"/> (mirroring how the
/// library creates its instruments in production). Each instance owns an isolated service provider, so the
/// underlying <c>Meter</c> is released on <see cref="Dispose"/> and never leaks across tests.
/// </summary>
internal sealed class TestMetrics : IDisposable
{
    private readonly ServiceProvider _provider;

    public TestMetrics()
    {
        _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        Metrics = new MediatorMetrics(_provider.GetRequiredService<IMeterFactory>());
    }

    public MediatorMetrics Metrics { get; }

    public void Dispose() => _provider.Dispose();
}
