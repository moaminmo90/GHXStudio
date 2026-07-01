using System;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using GHXStudio.Core.Services;
using GHXStudio.Core.Models;

// Explicit Aliasing
using Form = Eto.Forms.Form;
using Size = Eto.Drawing.Size;
using Font = Eto.Drawing.Font;
using Color = Eto.Drawing.Color;
using Binding = Eto.Forms.Binding;
using Label = Eto.Forms.Label;
using Button = Eto.Forms.Button;
using Orientation = Eto.Forms.Orientation;
using SaveFileDialog = Eto.Forms.SaveFileDialog;
using MessageBox = Eto.Forms.MessageBox;
using DialogResult = Eto.Forms.DialogResult;
using MessageBoxButtons = Eto.Forms.MessageBoxButtons;
using FileFilter = Eto.Forms.FileFilter;
using TableLayout = Eto.Forms.TableLayout;
using TableRow = Eto.Forms.TableRow;
using TableCell = Eto.Forms.TableCell;

namespace GHXStudio.Core.UI;

/// <summary>
/// The ultimate GHX Studio Dashboard.
/// Includes Solve Summary, Real-time Grid, Deep Inspection, and Multi-format Exports.
/// </summary>
public sealed class DashboardWindow : Form
{
    private readonly GridView _metricsGrid;
    private readonly Label _summaryLabel;

    public DashboardWindow()
    {
        Title = "GHX Studio - Professional DevTools";
        ClientSize = new Size(1000, 500); 
        Topmost = true;

        _summaryLabel = new Label { Font = new Font(SystemFont.Bold, 11), TextColor = Color.FromArgb(50, 50, 200) };

        _metricsGrid = new GridView
        {
            ShowHeader = true,
            GridLines = GridLines.Horizontal,
            AllowMultipleSelection = false
        };

        _metricsGrid.Columns.Add(new GridColumn { HeaderText = "Node Name", DataCell = new TextBoxCell { Binding = Binding.Property<NodeTelemetryRecord, string>(r => r.NodeName) }, Width = 150 });
        _metricsGrid.Columns.Add(new GridColumn { HeaderText = "Time (ms)", DataCell = new TextBoxCell { Binding = Binding.Property<NodeTelemetryRecord, string>(r => r.ExecutionTimeMs.ToString("F1")) }, Width = 80 });
        _metricsGrid.Columns.Add(new GridColumn { HeaderText = "Memory (MB)", DataCell = new TextBoxCell { Binding = Binding.Property<NodeTelemetryRecord, string>(r => r.MemoryEstimateMb.ToString("F2")) }, Width = 80 });
        _metricsGrid.Columns.Add(new GridColumn { HeaderText = "Static Analysis / Linter", DataCell = new TextBoxCell { Binding = Binding.Property<NodeTelemetryRecord, string>(r => RuleEngineService.EvaluateNode(r)) }, Width = 600 });

        var buttonLayout = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Items = 
            {
                CreateRefreshButton(),
                CreateToggleHeatmapButton(),
                CreateInspectButton(),
                CreateExportButton()
            }
        };

        // NEW ROBUST LAYOUT: Guarantees the GridView stays contained and shows scrollbars for massive definitions
        var layout = new TableLayout
        {
            Padding = 10,
            Spacing = new Size(5, 10),
            Rows = 
            {
                new TableRow(new Label { Text = "📊 Real-time Execution Timeline & Debugger", Font = new Font(SystemFont.Bold, 14) }),
                new TableRow(_summaryLabel),
                new TableRow { Cells = { new TableCell(_metricsGrid, true) }, ScaleHeight = true }, // This line forces the scrollbar
                new TableRow(buttonLayout)
            }
        };
        
        Content = layout;
        
        RefreshData();
    }

    private Button CreateInspectButton()
    {
        var btn = new Button { Text = "🔍 Deep Inspect & Trace" };
        btn.Click += (s, e) => 
        {
            if (_metricsGrid.SelectedItem is not NodeTelemetryRecord selectedRecord)
            {
                MessageBox.Show(this, "Please select a node from the table first.", "Selection Required", MessageBoxButtons.OK);
                return;
            }

            var health = DebuggerService.InspectDataTree(selectedRecord.NodeId);
            var upstream = DebuggerService.TraceRootCause(selectedRecord.NodeId);
            int downstreamCount = DebuggerService.TraceDownstreamImpact(selectedRecord.NodeId, out var downstreamNodes);

            string report = $"--- SMART DATA INSPECTOR ---\n" +
                            $"Target Node: {selectedRecord.NodeName}\n" +
                            $"Data Type: {health.DominantDataType}\n" +
                            $"Tree Depth: {health.MaxTreeDepth} lavel(s)\n" +
                            $"Total Branches: {health.TotalBranches}\n" +
                            $"Total Items: {health.TotalItems}\n" +
                            $"Null Values: {health.NullCount}\n\n" +
                            $"--- UPSTREAM TRACE ---\n";

            if (upstream.FoundRootCause) report += $"⚠️ Root Cause: '{upstream.RootCauseNodeName}' ({upstream.StepsUpstream} steps upstream)\n";
            else report += $"✅ No upstream data corruption.\n";

            report += $"\n--- DOWNSTREAM IMPACT ---\n";
            report += $"🔗 This node directly or indirectly affects {downstreamCount} downstream node(s).\n";

            MessageBox.Show(this, report, $"GHX Debugger: {selectedRecord.NodeName}", MessageBoxButtons.OK);
        };
        return btn;
    }

    private Button CreateRefreshButton()
    {
        var btn = new Button { Text = "Refresh Metrics" };
        btn.Click += (s, e) => RefreshData();
        return btn;
    }

    private Button CreateToggleHeatmapButton()
    {
        var btn = new Button { Text = CanvasRendererService.IsHeatmapEnabled ? "Disable Canvas Heatmap" : "Enable Canvas Heatmap" };
        btn.Click += (s, e) => 
        {
            CanvasRendererService.IsHeatmapEnabled = !CanvasRendererService.IsHeatmapEnabled;
            btn.Text = CanvasRendererService.IsHeatmapEnabled ? "Disable Canvas Heatmap" : "Enable Canvas Heatmap";
            CanvasRendererService.RedrawCanvas(); 
        };
        return btn;
    }

    private Button CreateExportButton()
    {
        var btn = new Button { Text = "Export Report (JSON / HTML)" };
        btn.Click += (s, e) => 
        {
            var dialog = new SaveFileDialog { Title = "Export GHX Report", Filters = { new FileFilter("JSON Data", ".json"), new FileFilter("HTML Report", ".html") } };
            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                bool success = dialog.FileName.EndsWith(".json") ? ExportService.ExportToJson(dialog.FileName) : ExportService.ExportToHtml(dialog.FileName);
                if (success) MessageBox.Show(this, "Report exported successfully!", "Complete", MessageBoxButtons.OK);
            }
        };
        return btn;
    }

    private void RefreshData()
    {
        var snapshot = ProfilerService.GetCurrentSnapshot();
        _metricsGrid.DataStore = snapshot.Cast<object>();

        if (snapshot.Any())
        {
            double totalTime = snapshot.Sum(x => x.ExecutionTimeMs);
            double totalMem = snapshot.Sum(x => x.MemoryEstimateMb);
            int errors = snapshot.Sum(x => x.ErrorCount);
            
            _summaryLabel.Text = $"Total Solve Time: {totalTime:F1} ms  |  Executed Nodes: {snapshot.Count}  |  Memory Peak: {totalMem:F2} MB  |  Errors: {errors}";
        }
        else
        {
            _summaryLabel.Text = "No graph data available. Please compute a Grasshopper graph.";
        }
    }
}