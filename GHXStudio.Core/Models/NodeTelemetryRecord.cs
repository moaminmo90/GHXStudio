using System;

namespace GHXStudio.Core.Models;

/// <summary>
/// Represents an immutable snapshot of a GH2 node's performance metrics 
/// during a single solver iteration.
/// </summary>
public readonly record struct NodeTelemetryRecord(
    Guid NodeId,
    string NodeName,
    string NodeType,
    double ExecutionTimeMs,
    long MemoryEstimateBytes,
    int WarningCount,
    int ErrorCount,
    int ThreadId
)
{
    // Computed property for UI rendering
    public double MemoryEstimateMb => MemoryEstimateBytes / (1024.0 * 1024.0);
    
    // Status flag based on predefined thresholds (e.g., > 500ms is a bottleneck)
    public bool IsBottleneck => ExecutionTimeMs > 500.0;
}