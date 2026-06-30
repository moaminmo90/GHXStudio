using System;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// Native telemetry adapter for Grasshopper 2.
/// Architecture is staged and awaits the official McNeel GH2 Kernel SDK public events.
/// </summary>
public static class GH2CanvasObserver
{
    private static bool _isAttached = false;

    public static void Attach()
    {
        if (_isAttached) return;

        // GH2 WIP API currently restricts public observer hooks to the Graph solution lifecycle.
        // Adapter is securely staged. Once the GH2 API stabilizes, dynamic hooks will be injected here.
        Rhino.RhinoApp.WriteLine("GHX Studio [Telemetry]: Grasshopper 2 Universal Adapter staged. Awaiting Official SDK.");
        
        _isAttached = true;
    }

    public static void RecordGH2Node(object gh2Node, double executionTimeMs)
    {
        // Failsafe telemetry drop until API unseals
    }
}