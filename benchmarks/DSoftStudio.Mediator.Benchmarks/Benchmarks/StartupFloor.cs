// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

namespace Benchmarks;

/// <summary>
/// The service the startup baseline registers and resolves, so that what MSDI costs is measured in
/// the baseline rather than charged to whichever library the row belongs to.
/// <para>
/// A baseline that only BUILDS a provider leaves the first <c>GetRequiredService</c> unmeasured, and
/// resolution is where a large part of the container's own cost lives: measured on a container
/// holding one trivial service and nothing else, building took 6.80 ms and the first resolve took
/// 5.44 ms. Without a resolve in the baseline, those 5.44 ms land in the library's column — which is
/// how this library's startup cost first read as 28.5 ms when it is closer to 9.
/// </para>
/// <para>
/// Deliberately the smallest thing a container can hold: one interface, one implementation, no
/// constructor parameters. What is being measured is the engine warming up, not the work.
/// </para>
/// </summary>
public interface IStartupFloor
{
    int Value { get; }
}

/// <inheritdoc cref="IStartupFloor"/>
public sealed class StartupFloor : IStartupFloor
{
    public int Value => 1;
}
