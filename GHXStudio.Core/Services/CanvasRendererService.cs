using System;
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// Intercepts the Grasshopper canvas painting pipeline to inject real-time 
/// performance heatmaps and execution time overlays directly on the nodes.
/// Implements robust lifecycle management to prevent premature hooks before GH loads.
/// </summary>
public static class CanvasRendererService
{
    public static bool IsHeatmapEnabled { get; set; } = false;
    private static bool _isAttached = false;

    // Standardized performance palette
    private static readonly Color FastColor = Color.FromArgb(60, 46, 204, 113);    // Transparent Green
    private static readonly Color WarningColor = Color.FromArgb(60, 241, 196, 15); // Transparent Yellow
    private static readonly Color CriticalColor = Color.FromArgb(80, 231, 76, 60); // Transparent Red

    public static void Initialize()
    {
        if (_isAttached) return;

        // If Grasshopper UI is already open when the plugin loads, attach immediately
        if (Instances.ActiveCanvas != null)
        {
            AttachToCanvas(Instances.ActiveCanvas);
        }

        // Subscribe to the global canvas creation event to catch future GH instances
        Instances.CanvasCreated += OnCanvasCreated;

        _isAttached = true;
    }

    private static void OnCanvasCreated(GH_Canvas canvas)
    {
        AttachToCanvas(canvas);
    }

    private static void AttachToCanvas(GH_Canvas canvas)
    {
        // Unsubscribe first to ensure we don't cause duplicate rendering artifacts
        canvas.CanvasPostPaintObjects -= OnCanvasPostPaintObjects;
        canvas.CanvasPostPaintObjects += OnCanvasPostPaintObjects;
    }

    private static void OnCanvasPostPaintObjects(GH_Canvas sender)
    {
        if (!IsHeatmapEnabled || sender.Document == null) return;

        var snapshot = ProfilerService.GetCurrentSnapshot();
        if (!snapshot.Any()) return;

        // Create a fast lookup dictionary to match canvas objects with their telemetry data
        var telemetryLookup = snapshot.ToDictionary(r => r.NodeId);

        using var overlayFont = new Font(new FontFamily("Arial"), 7f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.Black);
        using var textBgBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));

        foreach (var obj in sender.Document.Objects)
        {
            if (!telemetryLookup.TryGetValue(obj.InstanceGuid, out var record)) continue;

            // 1. Determine Heatmap Color based on execution time thresholds
            Color overlayColor;
            if (record.ExecutionTimeMs < 5.0) overlayColor = FastColor;
            else if (record.ExecutionTimeMs < 20.0) overlayColor = WarningColor;
            else overlayColor = CriticalColor;

            var nodeBounds = obj.Attributes.Bounds;

            // 2. Draw the Heatmap overlay via Grasshopper's GDI+ graphics engine
            using (var heatmapBrush = new SolidBrush(overlayColor))
            {
                sender.Graphics.FillRectangle(heatmapBrush, nodeBounds.X, nodeBounds.Y, nodeBounds.Width, nodeBounds.Height);
            }

            // 3. Draw the Performance Overlay Tag
            string overlayText = $"{record.ExecutionTimeMs:F1} ms";
            var textSize = sender.Graphics.MeasureString(overlayText, overlayFont);
            
            var tagRect = new RectangleF(
                nodeBounds.Left, 
                nodeBounds.Top - textSize.Height - 2, 
                textSize.Width + 4, 
                textSize.Height + 2);

            sender.Graphics.FillRectangle(textBgBrush, tagRect.X, tagRect.Y, tagRect.Width, tagRect.Height);
            sender.Graphics.DrawString(overlayText, overlayFont, textBrush, tagRect.Location);
        }
    }

    /// <summary>
    /// Forces the Grasshopper canvas to redraw immediately to reflect toggle state changes.
    /// </summary>
    public static void RedrawCanvas()
    {
        Instances.ActiveCanvas?.Invalidate();
    }
}