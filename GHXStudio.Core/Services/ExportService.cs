using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// Handles serialization of performance telemetry into JSON and HTML formats.
/// </summary>
public static class ExportService
{
    public static bool ExportToJson(string filePath)
    {
        try
        {
            var snapshot = ProfilerService.GetCurrentSnapshot();
            if (!snapshot.Any()) return false;

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(snapshot, options);
            
            File.WriteAllText(filePath, jsonString, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Rhino.RhinoApp.WriteLine($"GHX Studio [JSON Export Error]: {ex.Message}");
            return false;
        }
    }

    public static bool ExportToHtml(string filePath)
    {
        try
        {
            var snapshot = ProfilerService.GetCurrentSnapshot();
            if (!snapshot.Any()) return false;

            var html = new StringBuilder();
            html.AppendLine("<html><head><title>GHX Studio Report</title>");
            html.AppendLine("<style>body{font-family:Arial,sans-serif; margin:20px;} table{width:100%; border-collapse:collapse;} th,td{border:1px solid #ddd; padding:8px;} th{background-color:#4CAF50; color:white;}</style>");
            html.AppendLine("</head><body><h2>GHX Studio Performance Report</h2>");
            html.AppendLine("<table><tr><th>Node</th><th>Type</th><th>Time (ms)</th><th>Memory (MB)</th><th>Warnings</th><th>Errors</th></tr>");

            foreach (var r in snapshot)
            {
                html.AppendLine($"<tr><td>{r.NodeName}</td><td>{r.NodeType}</td><td>{r.ExecutionTimeMs:F2}</td><td>{r.MemoryEstimateMb:F4}</td><td>{r.WarningCount}</td><td>{r.ErrorCount}</td></tr>");
            }

            html.AppendLine("</table></body></html>");
            File.WriteAllText(filePath, html.ToString(), Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Rhino.RhinoApp.WriteLine($"GHX Studio [HTML Export Error]: {ex.Message}");
            return false;
        }
    }
}