// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Configs;

namespace Benchmarks;

/// <summary>
/// Adds <see cref="AddedOverFloorColumn"/> to the startup suites. A config rather than a bare
/// attribute because BenchmarkDotNet takes custom columns only through one.
/// </summary>
public sealed class AddedOverFloorConfig : ManualConfig
{
    public AddedOverFloorConfig() => AddColumn(AddedOverFloorColumn.Instance);
}
