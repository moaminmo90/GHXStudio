using System;

namespace GHXStudio.Core.Models;

/// <summary>
/// Represents the statistical outcome of a multi-iteration benchmark run,
/// including advanced Garbage Collection (GC) memory leak telemetry.
/// </summary>
public readonly record struct BenchmarkResult(
    int TotalIterations,
    int ValidIterations,
    double MeanExecutionTimeMs,
    double StandardDeviationMs,
    double MinTimeMs,
    double MaxTimeMs,
    double MemoryLeakMb
);