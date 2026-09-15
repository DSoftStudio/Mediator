// Copyright (c) DSoftStudio. All rights reserved.
// Licensed under the MIT License.

namespace Benchmarks;

/// <summary>
/// A benchmark that measures nothing still produces a number, and a fast one at that — which is the
/// worst possible failure because it looks like a result.
/// <para>
/// Every suite asserts, in its <c>[GlobalSetup]</c>, that the thing it claims to measure actually
/// happened: the handler ran, the behaviors fired, the notification reached its subscribers, the
/// stream produced items. A banner printed to the console does not do that — nobody reads a banner in
/// a twenty-minute run, and BenchmarkDotNet reports the suite as successful either way. These throw,
/// so a broken setup stops the run instead of publishing a fiction.
/// </para>
/// <para>
/// It lives outside the measured path by construction: <c>[GlobalSetup]</c> runs once, before any
/// iteration. Counting inside a measured behavior costs 3.78 ns per link — enough that a suite which
/// verified that way was 19 ns slower than one that did not, and the comparison table read it as a
/// difference between libraries.
/// </para>
/// </summary>
internal static class BenchmarkVerification
{
    /// <summary>Aborts the run when the setup did not produce what the suite is about to measure.</summary>
    public static void Require(bool condition, string suite, string what)
    {
        if (condition)
            return;

        throw new InvalidOperationException(
            $"{suite}: {what}. The benchmark would report a number for work that never happened — aborting.");
    }

    /// <summary>Aborts unless <paramref name="actual"/> matches, naming both values.</summary>
    public static void RequireCount(int actual, int expected, string suite, string what)
        => Require(actual == expected, suite, $"{what} ran {actual} time(s), expected {expected}");
}
