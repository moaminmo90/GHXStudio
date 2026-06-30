using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rhino;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// Thread-safe telemetry aggregator for Grasshopper 2 execution tracking.
/// </summary>
public static class ProfilerService
{
    private static bool _isInitialized;
    private static readonly Stopwatch _globalSolveTimer = new();
    
    // Concurrent dictionary handles multi-threaded inserts from GH2 worker threads natively.
    private static readonly ConcurrentDictionary<Guid, NodeTelemetryRecord> _nodeMetrics = new();

    public static void Initialize()
    {
        if (_isInitialized) return;
        
        _nodeMetrics.Clear();
        _isInitialized = true;
        
        RhinoApp.WriteLine("GHX Studio [Service]: High-performance telemetry aggregator initialized.");
    }

    /// <summary>
    /// Registers or updates telemetry for a specific node post-solve.
    /// Called directly by our GH2 node hooks.
    /// </summary>
    public static void RecordNodeExecution(NodeTelemetryRecord record)
    {
        _nodeMetrics.AddOrUpdate(record.NodeId, record, (id, existing) => record);
    }

    public static void BeginGlobalSolve()
    {
        _nodeMetrics.Clear();
        _globalSolveTimer.Restart();
    }

    public static void EndGlobalSolve()
    {
        _globalSolveTimer.Stop();
        
        // Dispatch the analysis pipeline once the graph finishes calculating
        AnalyzeSolveIteration();
    }

    private static void AnalyzeSolveIteration()
    {
        var totalSolveTime = _globalSolveTimer.ElapsedMilliseconds;
        var executedNodesCount = _nodeMetrics.Count;
        
        if (executedNodesCount == 0) return;

        // LINQ query to extract bottlenecks (Top 10 Slowest Nodes) - referencing PDF Section 5.3
        var bottlenecks = _nodeMetrics.Values
            .Where(n => n.IsBottleneck || n.ExecutionTimeMs > 0)
            .OrderByDescending(n => n.ExecutionTimeMs)
            .Take(10)
            .ToList();

        RhinoApp.WriteLine($"\n--- GHX SOLVE SUMMARY ---");
        RhinoApp.WriteLine($"Total Solve Time: {totalSolveTime} ms");
        RhinoApp.WriteLine($"Nodes Executed: {executedNodesCount}");
        
        if (bottlenecks.Any())
        {
            var slowest = bottlenecks.First();
            RhinoApp.WriteLine($"CRITICAL: Slowest Node is '{slowest.NodeName}' ({slowest.ExecutionTimeMs} ms) on Thread-{slowest.ThreadId}");
        }
        RhinoApp.WriteLine("---------------------------\n");
    }
    
    /// <summary>
    /// Exposes a read-only snapshot of current metrics for UI binding (Eto.Forms).
    /// </summary>
    public static IReadOnlyCollection<NodeTelemetryRecord> GetCurrentSnapshot()
    {
        return _nodeMetrics.Values.ToList().AsReadOnly();
    }
}