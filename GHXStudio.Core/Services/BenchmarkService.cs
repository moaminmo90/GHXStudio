using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// Enterprise-grade asynchronous benchmarking & Memory Profiler engine.
/// Executes the Grasshopper definition cooperatively and measures deep GC allocations.
/// </summary>
public static class BenchmarkService
{
    public static bool IsRunning { get; private set; }

    public static async Task<BenchmarkResult?> RunStatisticalBenchmarkAsync(int iterations = 10, Action<int>? progressCallback = null)
    {
        var doc = Instances.ActiveCanvas?.Document;
        if (doc == null || iterations < 3) return null;

        if (IsRunning) return null;
        IsRunning = true;

        var executionTimes = new List<double>();
        var globalStopwatch = new Stopwatch();

        try
        {
            // --- 1. BASELINE MEMORY MEASUREMENT ---
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            long memoryBeforeRun = GC.GetTotalMemory(true);

            // --- 2. EXECUTION LOOP ---
            for (int i = 1; i <= iterations; i++)
            {
                progressCallback?.Invoke(i);

                foreach (var obj in doc.Objects.OfType<IGH_ActiveObject>())
                {
                    obj.ExpireSolution(false);
                }

                globalStopwatch.Restart();
                doc.NewSolution(false);
                globalStopwatch.Stop();

                executionTimes.Add(globalStopwatch.Elapsed.TotalMilliseconds);

                // Yield control to OS to prevent Rhino UI freeze
                await Task.Delay(50);
            }

            // --- 3. POST-EXECUTION MEMORY MEASUREMENT ---
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            long memoryAfterRun = GC.GetTotalMemory(true);

            double leakDeltaMb = (memoryAfterRun - memoryBeforeRun) / (1024.0 * 1024.0);
            if (leakDeltaMb < 0) leakDeltaMb = 0;

            return CalculateMetrics(executionTimes, leakDeltaMb);
        }
        catch (Exception ex)
        {
            Rhino.RhinoApp.WriteLine($"[GHX Studio] Benchmark failed: {ex.Message}");
            return null;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static BenchmarkResult CalculateMetrics(List<double> rawTimes, double memoryLeakMb)
    {
        var sortedTimes = rawTimes.OrderBy(t => t).ToList();

        if (sortedTimes.Count >= 5)
        {
            sortedTimes.RemoveAt(0); 
            sortedTimes.RemoveAt(sortedTimes.Count - 1); 
        }

        int validCount = sortedTimes.Count;
        double mean = sortedTimes.Average();
        
        double sumOfSquares = sortedTimes.Sum(t => Math.Pow(t - mean, 2));
        double standardDeviation = Math.Sqrt(sumOfSquares / validCount);

        return new BenchmarkResult(
            TotalIterations: rawTimes.Count,
            ValidIterations: validCount,
            MeanExecutionTimeMs: mean,
            StandardDeviationMs: standardDeviation,
            MinTimeMs: sortedTimes.First(),
            MaxTimeMs: sortedTimes.Last(),
            MemoryLeakMb: memoryLeakMb
        );
    }
}