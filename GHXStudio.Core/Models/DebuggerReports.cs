using System;
using System.Collections.Generic;

namespace GHXStudio.Core.Models;

/// <summary>
/// Represents the statistical health of a Grasshopper Data Tree.
/// </summary>
public readonly record struct DataTreeHealthReport(
    int TotalBranches,
    int TotalItems,
    int NullCount,
    int EmptyBranchCount,
    int MaxTreeDepth,
    string DominantDataType
);

/// <summary>
/// Represents the result of an upstream dependency trace for error isolation.
/// </summary>
public readonly record struct UpstreamTraceResult(
    bool FoundRootCause,
    Guid RootCauseNodeId,
    string RootCauseNodeName,
    int StepsUpstream,
    string ProbableCauseDescription,
    List<Guid> TracePath
);