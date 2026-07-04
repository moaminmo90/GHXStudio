using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// Intercepts the Grasshopper canvas painting pipeline to inject real-time 
/// performance heatmaps and glowing visual error traces directly on the nodes.
/// </summary>
public static class CanvasRendererService
{
    public static bool IsHeatmapEnabled { get; set; } = false;
    public static List<Guid> ActiveTracePath { get; set; } = new();
    private static bool _isAttached = false;

    private static readonly Color FastColor = Color.FromArgb(60, 46, 204, 113);
    private static readonly Color WarningColor = Color.FromArgb(60, 241, 196, 15);
    private static readonly Color CriticalColor = Color.FromArgb(80, 231, 76, 60);

    public static void Initialize()
    {
        if (_isAttached) return;

        if (Instances.ActiveCanvas != null)
            AttachToCanvas(Instances.ActiveCanvas);

        Instances.CanvasCreated += OnCanvasCreated;
        _isAttached = true;
    }

    private static void OnCanvasCreated(GH_Canvas canvas)
    {
        AttachToCanvas(canvas);
    }

    private static void AttachToCanvas(GH_Canvas canvas)
    {
        canvas.CanvasPostPaintObjects -= OnCanvasPostPaintObjects;
        canvas.CanvasPostPaintObjects += OnCanvasPostPaintObjects;
    }

    private static void OnCanvasPostPaintObjects(GH_Canvas sender)
    {
        if (sender.Document == null) return;

        // --- 1. RENDER VISUAL ERROR TRACE (GLOWING WIRES) ---
        if (ActiveTracePath != null && ActiveTracePath.Count > 1)
        {
            using var glowOuter = new Pen(Color.FromArgb(40, 255, 100, 0), 14f) { DashStyle = DashStyle.Solid, StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var glowInner = new Pen(Color.FromArgb(120, 255, 150, 0), 6f) { DashStyle = DashStyle.Solid, StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var coreWire = new Pen(Color.FromArgb(255, 255, 200, 0), 2f) { DashStyle = DashStyle.Dash, CustomEndCap = new AdjustableArrowCap(5, 5) };

            for (int i = 0; i < ActiveTracePath.Count - 1; i++)
            {
                var nodeA = sender.Document.FindObject(ActiveTracePath[i], false);
                var nodeB = sender.Document.FindObject(ActiveTracePath[i + 1], false);
                if (nodeA == null || nodeB == null) continue;

                var boundsA = nodeA.Attributes.Bounds;
                var boundsB = nodeB.Attributes.Bounds;

                PointF ptA = new PointF(boundsA.Right, boundsA.Top + boundsA.Height / 2);
                PointF ptB = new PointF(boundsB.Left, boundsB.Top + boundsB.Height / 2);

                float dx = Math.Max(50f, (ptB.X - ptA.X) * 0.5f);
                PointF cpA = new PointF(ptA.X + dx, ptA.Y);
                PointF cpB = new PointF(ptB.X - dx, ptB.Y);

                sender.Graphics.DrawBezier(glowOuter, ptA, cpA, cpB, ptB);
                sender.Graphics.DrawBezier(glowInner, ptA, cpA, cpB, ptB);
                sender.Graphics.DrawBezier(coreWire, ptA, cpA, cpB, ptB);
            }
        }

        // --- 2. RENDER PERFORMANCE HEATMAP ---
        if (!IsHeatmapEnabled) return;

        var snapshot = ProfilerService.GetCurrentSnapshot();
        if (!snapshot.Any()) return;

        var telemetryLookup = snapshot.ToDictionary(r => r.NodeId);
        using var overlayFont = new Font(new FontFamily("Arial"), 7f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.Black);
        using var textBgBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));

        foreach (var obj in sender.Document.Objects)
        {
            if (!telemetryLookup.TryGetValue(obj.InstanceGuid, out var record)) continue;

            Color overlayColor;
            if (record.ExecutionTimeMs < 5.0) overlayColor = FastColor;
            else if (record.ExecutionTimeMs < 20.0) overlayColor = WarningColor;
            else overlayColor = CriticalColor;

            var nodeBounds = obj.Attributes.Bounds;

            using (var heatmapBrush = new SolidBrush(overlayColor))
            {
                sender.Graphics.FillRectangle(heatmapBrush, nodeBounds.X, nodeBounds.Y, nodeBounds.Width, nodeBounds.Height);
            }

            string overlayText = $"{record.ExecutionTimeMs:F1} ms";
            var textSize = sender.Graphics.MeasureString(overlayText, overlayFont);
            
            var tagRect = new RectangleF(nodeBounds.Left, nodeBounds.Top - textSize.Height - 2, textSize.Width + 4, textSize.Height + 2);
            sender.Graphics.FillRectangle(textBgBrush, tagRect.X, tagRect.Y, tagRect.Width, tagRect.Height);
            sender.Graphics.DrawString(overlayText, overlayFont, textBrush, tagRect.Location);
        }
    }

    public static void RedrawCanvas()
    {
        Instances.ActiveCanvas?.Invalidate();
    }
}