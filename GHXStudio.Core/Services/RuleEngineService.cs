using System;
using System.Collections.Generic;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// The core Linter engine. Analyzes node telemetry against predefined performance heuristics
/// to detect bad practices and architectural bottlenecks in the Grasshopper graph.
/// </summary>
public static class RuleEngineService
{
    public static string EvaluateNode(NodeTelemetryRecord node)
    {
        var warnings = new List<string>();

        // Rule 1: Slow Node Detection (Threshold: 20ms for real-time responsiveness)
        if (node.ExecutionTimeMs > 20.0)
        {
            warnings.Add($"[Rule 1] Performance bottleneck: Execution time ({node.ExecutionTimeMs:F1}ms) is suboptimal.");
        }

        // Rule 2: Memory Heavy Node (Threshold: 5MB)
        if (node.MemoryEstimateMb > 5.0)
        {
            warnings.Add($"[Rule 2] High memory footprint: Consuming {node.MemoryEstimateMb:F2}MB.");
        }

        // Rule 3: Native Error/Warning pass-through
        if (node.ErrorCount > 0)
        {
            warnings.Add("[Rule 3] Critical: Node failed during runtime computation.");
        }

        return warnings.Count > 0 ? string.Join(" | ", warnings) : "Optimal";
    }
}