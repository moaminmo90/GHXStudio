using System;
using System.Collections.Generic;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// Advanced static analysis and deep graph traversal engine.
/// Provides Data Tree inspection, Upstream error tracing, and Downstream impact analysis.
/// </summary>
public static class DebuggerService
{
    public static DataTreeHealthReport InspectDataTree(Guid nodeId)
    {
        var doc = Instances.ActiveCanvas?.Document;
        if (doc == null) return default;

        var obj = doc.FindObject(nodeId, false) as IGH_Component;
        if (obj == null || obj.Params.Output.Count == 0) return default;

        int totalBranches = 0; int totalItems = 0; int nullCount = 0;
        int emptyBranchCount = 0; int maxDepth = 0; string dataType = "Unknown";

        foreach (var outputParam in obj.Params.Output)
        {
            var dataTree = outputParam.VolatileData;
            totalBranches += dataTree.PathCount;

            foreach (GH_Path path in dataTree.Paths)
            {
                if (path.Length > maxDepth) maxDepth = path.Length;
                
                var branch = dataTree.get_Branch(path);
                if (branch.Count == 0) emptyBranchCount++;
                totalItems += branch.Count;

                foreach (var item in branch)
                {
                    if (item == null) nullCount++;
                    else if (dataType == "Unknown" && item is IGH_Goo goo) dataType = goo.TypeName;
                }
            }
        }
        return new DataTreeHealthReport(totalBranches, totalItems, nullCount, emptyBranchCount, maxDepth, dataType);
    }

    public static UpstreamTraceResult TraceRootCause(Guid targetNodeId)
    {
        var doc = Instances.ActiveCanvas?.Document;
        if (doc == null) return new UpstreamTraceResult(false, Guid.Empty, string.Empty, 0, "No active document.");

        var targetObj = doc.FindObject(targetNodeId, false) as IGH_Component;
        if (targetObj == null) return new UpstreamTraceResult(false, Guid.Empty, string.Empty, 0, "Node not found.");

        var queue = new Queue<(IGH_Component Node, int Depth)>();
        var visited = new HashSet<Guid>();

        queue.Enqueue((targetObj, 0));
        visited.Add(targetObj.InstanceGuid);

        while (queue.Count > 0)
        {
            var (currentNode, depth) = queue.Dequeue();

            if (depth > 0)
            {
                if (currentNode.RuntimeMessageLevel == GH_RuntimeMessageLevel.Error || currentNode.RuntimeMessageLevel == GH_RuntimeMessageLevel.Warning)
                    return new UpstreamTraceResult(true, currentNode.InstanceGuid, currentNode.NickName, depth, "Upstream node failed natively.");

                var health = InspectDataTree(currentNode.InstanceGuid);
                if (health.NullCount > 0)
                    return new UpstreamTraceResult(true, currentNode.InstanceGuid, currentNode.NickName, depth, $"Generated {health.NullCount} Null values cascading downstream.");
            }

            foreach (var inputParam in currentNode.Params.Input)
            {
                foreach (var source in inputParam.Sources)
                {
                    if (source.Attributes.GetTopLevel.DocObject is IGH_Component sourceComponent && !visited.Contains(sourceComponent.InstanceGuid))
                    {
                        visited.Add(sourceComponent.InstanceGuid);
                        queue.Enqueue((sourceComponent, depth + 1));
                    }
                }
            }
        }
        return new UpstreamTraceResult(false, targetObj.InstanceGuid, targetObj.NickName, 0, "No external root cause found.");
    }

    /// <summary>
    /// Executes a Breadth-First Search (BFS) to determine how many downstream nodes depend on this node's output.
    /// </summary>
    public static int TraceDownstreamImpact(Guid targetNodeId, out List<string> impactedNodeNames)
    {
        impactedNodeNames = new List<string>();
        var doc = Instances.ActiveCanvas?.Document;
        if (doc == null) return 0;

        var targetObj = doc.FindObject(targetNodeId, false) as IGH_Component;
        if (targetObj == null) return 0;

        var queue = new Queue<IGH_Component>();
        var visited = new HashSet<Guid>();

        queue.Enqueue(targetObj);
        visited.Add(targetObj.InstanceGuid);

        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();
            
            if (currentNode.InstanceGuid != targetNodeId)
            {
                impactedNodeNames.Add(currentNode.NickName);
            }

            foreach (var outputParam in currentNode.Params.Output)
            {
                foreach (var recipient in outputParam.Recipients)
                {
                    if (recipient.Attributes.GetTopLevel.DocObject is IGH_Component recipientComponent && !visited.Contains(recipientComponent.InstanceGuid))
                    {
                        visited.Add(recipientComponent.InstanceGuid);
                        queue.Enqueue(recipientComponent);
                    }
                }
            }
        }
        return impactedNodeNames.Count;
    }
}