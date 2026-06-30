using System;
using Rhino.PlugIns;
using GHXStudio.Core.Services;

[assembly: PlugInDescription(DescriptionType.Organization, "Mohammad Amin Moradi")]
[assembly: PlugInDescription(DescriptionType.WebSite, "https://github.com/moaminmo90")]
[assembly: PlugInDescription(DescriptionType.Email, "https://www.linkedin.com/in/moaminmo90")]
[assembly: PlugInDescription(DescriptionType.Icon, "GHXStudio.Core.icon.png")]

namespace GHXStudio.Core;

/// <summary>
/// The core entry point for the Rhino plugin architecture.
/// Handles the initialization and lifecycle of telemetry adapters and rendering resources.
/// </summary>
public sealed class GHXStudioPlugin : PlugIn
{
    public static GHXStudioPlugin Instance { get; private set; } = null!;

    public GHXStudioPlugin()
    {
        Instance = this;
    }

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        Rhino.RhinoApp.WriteLine("GHX Studio [Core]: Initialized successfully. Created by Mohammad Amin Moradi.");
        
        ProfilerService.Initialize();
        
        CanvasObserver.Attach();
        GH2CanvasObserver.Attach();
        
        CanvasRendererService.Initialize();
        
        return LoadReturnCode.Success;
    }
}