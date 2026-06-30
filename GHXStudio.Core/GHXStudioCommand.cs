using System;
using Rhino;
using Rhino.Commands;

namespace GHXStudio.Core;

/// <summary>
/// The main Rhino command to launch the GHX Studio dashboard.
/// </summary>
public sealed class GHXStudioCommand : Command
{
    public static GHXStudioCommand Instance { get; private set; } = null!;

    public GHXStudioCommand()
    {
        Instance = this;
    }

    public override string EnglishName => "GHXStudio";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        RhinoApp.WriteLine("GHX Studio: Launching Professional DevTools Dashboard...");
        
        var dashboard = new UI.DashboardWindow();
        dashboard.Show();

        return Result.Success;
    }
}