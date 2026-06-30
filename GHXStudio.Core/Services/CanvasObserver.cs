using System;
using System.Diagnostics;
using Grasshopper;
using Grasshopper.Kernel;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// Subscribes to the active Grasshopper document and intercepts execution 
/// events to harvest precise telemetry for each node.
/// </summary>
public static class CanvasObserver
{
    private static bool _isAttached;

    public static void Attach()
    {
        if (_isAttached) return;

        Instances.DocumentServer.DocumentAdded += OnDocumentAdded;
        
        foreach (GH_Document doc in Instances.DocumentServer)
        {
            if (doc != null)
            {
                AttachToDocument(doc);
            }
        }

        _isAttached = true;
        Rhino.RhinoApp.WriteLine("GHX Studio [Telemetry]: Canvas observer successfully attached to Grasshopper Kernel.");
    }

    private static void OnDocumentAdded(GH_DocumentServer sender, GH_Document doc)
    {
        AttachToDocument(doc);
    }

    private static void AttachToDocument(GH_Document doc)
    {
        doc.SolutionStart -= OnSolutionStart;
        doc.SolutionEnd -= OnSolutionEnd;

        doc.SolutionStart += OnSolutionStart;
        doc.SolutionEnd += OnSolutionEnd;

        // INITIAL HARVEST: Extract the performance state of the canvas immediately 
        // upon attachment, so the user doesn't have to recompute the graph.
        HarvestDocumentTelemetry(doc);
        ProfilerService.EndGlobalSolve();
    }

    private static void OnSolutionStart(object? sender, GH_SolutionEventArgs e)
    {
        ProfilerService.BeginGlobalSolve();
    }

    private static void OnSolutionEnd(object? sender, GH_SolutionEventArgs e)
    {
        if (sender is not GH_Document doc) return;

        HarvestDocumentTelemetry(doc);
        ProfilerService.EndGlobalSolve();
    }

    /// <summary>
    /// Scans the document and records the processor time for all computed active objects.
    /// </summary>
    private static void HarvestDocumentTelemetry(GH_Document doc)
    {
        foreach (var obj in doc.Objects)
        {
            if (obj is not IGH_ActiveObject activeObj) continue;

            var executionTimeMs = activeObj.ProcessorTime.TotalMilliseconds;
            
            if (executionTimeMs <= 0 && activeObj.Phase != GH_SolutionPhase.Computed) continue;

            var record = new NodeTelemetryRecord(
                NodeId: activeObj.InstanceGuid,
                NodeName: activeObj.NickName ?? activeObj.Name,
                NodeType: activeObj.Name,
                ExecutionTimeMs: executionTimeMs,
                MemoryEstimateBytes: EstimateMemoryFootprint(activeObj),
                WarningCount: activeObj.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count,
                ErrorCount: activeObj.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count,
                ThreadId: Environment.CurrentManagedThreadId
            );

            ProfilerService.RecordNodeExecution(record);
        }
    }

    private static long EstimateMemoryFootprint(IGH_ActiveObject obj)
    {
        long estimatedBytes = 0;
        
        if (obj is IGH_Component comp)
        {
            foreach (var param in comp.Params.Output)
            {
                estimatedBytes += param.VolatileDataCount * 64L;
            }
        }
        
        return estimatedBytes;
    }
}