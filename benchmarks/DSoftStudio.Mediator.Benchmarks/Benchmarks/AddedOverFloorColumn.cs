// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Mathematics;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Benchmarks;

/// <summary>
/// Reports each startup row MINUS the baseline row, so the table answers "what does this library
/// add" without the reader doing arithmetic.
/// <para>
/// Three rows already separate the DI floor from registration from the first dispatch, but a reader
/// still has to subtract to learn the only number that concerns them. They mostly will not, and the
/// total is the misleading one: on a container holding a single trivial service, .NET and the
/// container alone cost about 11.7 ms before any mediator exists. Published as a total, that reads
/// as the library's cost.
/// </para>
/// <para>
/// Ratio does not solve it either. "2.92x" over a floor nobody caused is a ratio of mostly-unrelated
/// work, and it flatters or damns depending on how slow the floor happened to be that day.
/// </para>
/// <para>
/// Uses the MEDIAN rather than the mean, matching the advice printed above these tables: startup is
/// measured one process per sample, and process timings are right-skewed enough that the mean of the
/// first row executed carries the machine's own file-cache warm-up.
/// </para>
/// </summary>
public sealed class AddedOverFloorColumn : BaselineCustomColumn
{
    public static readonly IColumn Instance = new AddedOverFloorColumn();

    public override string Id => nameof(AddedOverFloorColumn);

    public override string ColumnName => "Added";

    public override string Legend =>
        "Median of this row minus the median of the baseline row - what the library adds over the DI floor";

    public override int PriorityInCategory => 1;

    public override ColumnCategory Category => ColumnCategory.Baseline;

    // Not numeric: the cell reads "baseline" on the floor row, and a column that sometimes holds a
    // word cannot be right-aligned as a number without the exporters mangling it.
    public override bool IsNumeric => false;

    public override UnitType UnitType => UnitType.Time;

    public override string GetValue(
        Summary summary,
        BenchmarkCase benchmarkCase,
        Statistics baseline,
        IReadOnlyDictionary<string, Metric> baselineMetric,
        Statistics current,
        IReadOnlyDictionary<string, Metric> currentMetric,
        bool isBaseline)
    {
        // The baseline row is the floor itself: saying "+0.000 ms" there would invite reading it as a
        // measurement rather than as a definition.
        if (isBaseline)
            return "baseline";

        if (baseline is null || current is null)
            return "?";

        var addedNs = current.Median - baseline.Median;

        return string.Create(CultureInfo.InvariantCulture, $"{addedNs / 1_000_000.0:F2} ms");
    }
}
