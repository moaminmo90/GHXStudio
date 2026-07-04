using System;
using System.Linq;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Drawing;
using GHXStudio.Core.Services;
using GHXStudio.Core.Models;

// Explicit Aliasing to prevent any collisions
using Form = Eto.Forms.Form;
using Size = Eto.Drawing.Size;
using Font = Eto.Drawing.Font;
using Color = Eto.Drawing.Color;
using Binding = Eto.Forms.Binding;
using Label = Eto.Forms.Label;
using Button = Eto.Forms.Button;
using Orientation = Eto.Forms.Orientation;
using SaveFileDialog = Eto.Forms.SaveFileDialog;
using OpenFileDialog = Eto.Forms.OpenFileDialog;
using MessageBox = Eto.Forms.MessageBox;
using DialogResult = Eto.Forms.DialogResult;
using MessageBoxButtons = Eto.Forms.MessageBoxButtons;
using FileFilter = Eto.Forms.FileFilter;
using TableLayout = Eto.Forms.TableLayout;
using TableRow = Eto.Forms.TableRow;
using TableCell = Eto.Forms.TableCell;
using Application = Eto.Forms.Application;

namespace GHXStudio.Core.UI;

public sealed class DashboardWindow : Form
{
    private readonly GridView _metricsGrid;
    private readonly Label _summaryLabel;
    private readonly bool _isDarkMode;

    public DashboardWindow()
    {
        Title = "GHX Studio - Professional DevTools (v2.0)";
        ClientSize = new Size(1100, 500); 
        Topmost = true;

        // --- DARK MODE DETECTION ENGINE ---
        // Explicitly use Eto.Drawing.SystemColors to prevent collision
        var bg = Eto.Drawing.SystemColors.WindowBackground;
        
        // Luminance formula for Eto Colors (R, G, B are 0.0f to 1.0f)
        _isDarkMode = (bg.R * 0.299f + bg.G * 0.587f + bg.B * 0.114f) < 0.5f;

        BackgroundColor = bg;

        Color summaryTextColor = _isDarkMode ? Color.FromArgb(100, 200, 255) : Color.FromArgb(50, 50, 200);
        
        _summaryLabel = new Label { Font = new Font(SystemFont.Bold, 11), TextColor = summaryTextColor };

        _metricsGrid = new GridView
        {
            ShowHeader = true,
            GridLines = GridLines.Horizontal,
            AllowMultipleSelection = false
        };

        _metricsGrid.Columns.Add(new GridColumn { HeaderText = "Node Name", DataCell = new TextBoxCell { Binding = Binding.Property<NodeTelemetryRecord, string>(r => r.NodeName) }, Width = 150 });
        _metricsGrid.Columns.Add(new GridColumn { HeaderText = "Time (ms)", DataCell = new TextBoxCell { Binding = Binding.Property<NodeTelemetryRecord, string>(r => r.ExecutionTimeMs.ToString("F1")) }, Width = 80 });
        _metricsGrid.Columns.Add(new GridColumn { HeaderText = "Memory (MB)", DataCell = new TextBoxCell { Binding = Binding.Property<NodeTelemetryRecord, string>(r => r.MemoryEstimateMb.ToString("F2")) }, Width = 80 });
        _metricsGrid.Columns.Add(new GridColumn { HeaderText = "Static Analysis / Linter", DataCell = new TextBoxCell { Binding = Binding.Property<NodeTelemetryRecord, string>(r => RuleEngineService.EvaluateNode(r)) }, Width = 650 });

        var buttonLayout = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Items = 
            {
                CreateRefreshButton(),
                CreateToggleHeatmapButton(),
                CreateInspectButton(),
                CreateBenchmarkButton(),
                CreateLoadRulesButton(),
                CreateExportButton()
            }
        };

        var layout = new TableLayout
        {
            Padding = 10,
            Spacing = new Size(5, 10),
            Rows = 
            {
                new TableRow(new Label { Text = "📊 Real-time Execution Timeline & Debugger", Font = new Font(SystemFont.Bold, 14) }),
                new TableRow(_summaryLabel),
                new TableRow { Cells = { new TableCell(_metricsGrid, true) }, ScaleHeight = true },
                new TableRow(buttonLayout)
            }
        };
        
        Content = layout;
        RefreshData();
    }

    private Button CreateLoadRulesButton()
    {
        var btn = new Button { Text = "📜 Load BIM Rules (JSON)" };
        btn.Click += (s, e) => 
        {
            var dialog = new OpenFileDialog { Title = "Load BIM Linter Ruleset", Filters = { new FileFilter("JSON Ruleset", ".json") } };
            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                if (RuleEngineService.LoadCustomRules(dialog.FileName))
                {
                    MessageBox.Show(this, "Custom BIM Ruleset loaded and applied successfully!", "Linter Updated", MessageBoxButtons.OK);
                    RefreshData(); 
                }
                else
                {
                    MessageBox.Show(this, "Failed to parse JSON ruleset.", "Error", MessageBoxButtons.OK);
                }
            }
        };
        return btn;
    }

    private Button CreateBenchmarkButton()
    {
        var btn = new Button { Text = "⏱️ Run Benchmark (10x)" };
        btn.Click += async (s, e) => 
        {
            if (BenchmarkService.IsRunning) return;
            btn.Enabled = false;
            var originalText = btn.Text;

            var result = await BenchmarkService.RunStatisticalBenchmarkAsync(10, (currentIteration) => 
            {
                Application.Instance.AsyncInvoke(() => btn.Text = $"Running Benchmark & GC... ({currentIteration}/10)");
            });

            btn.Text = originalText;
            btn.Enabled = true;

            if (result.HasValue)
            {
                var r = result.Value;
                string leakReport = r.MemoryLeakMb > 0.5 
                    ? $"🚨 SYSTEM MEMORY LEAK DETECTED: {r.MemoryLeakMb:F2} MB of RAM was not freed."
                    : $"✅ No systemic memory leaks detected ({r.MemoryLeakMb:F2} MB variant).";

                string report = $"--- STATISTICAL BENCHMARK & MEMORY PROFILER ---\n\n" +
                                $"Total Runs: {r.TotalIterations} (Filtered {r.TotalIterations - r.ValidIterations} OS outliers)\n\n" +
                                $"🎯 True Mean Solve Time: {r.MeanExecutionTimeMs:F2} ms\n" +
                                $"📉 Standard Deviation: ±{r.StandardDeviationMs:F2} ms\n\n" +
                                $"--- GARBAGE COLLECTION ANALYSIS ---\n" +
                                $"{leakReport}";
                MessageBox.Show(this, report, "GHX Benchmark Complete", MessageBoxButtons.OK);
                RefreshData(); 
            }
            else MessageBox.Show(this, "Benchmark failed.", "Error", MessageBoxButtons.OK);
        };
        return btn;
    }

    private Button CreateInspectButton()
    {
        var btn = new Button { Text = "🔍 Deep Inspect & Visual Trace" };
        btn.Click += (s, e) => 
        {
            if (_metricsGrid.SelectedItem is not NodeTelemetryRecord selectedRecord)
            {
                MessageBox.Show(this, "Please select a node.", "Selection Required", MessageBoxButtons.OK);
                return;
            }

            var health = DebuggerService.InspectDataTree(selectedRecord.NodeId);
            var upstream = DebuggerService.TraceRootCause(selectedRecord.NodeId);
            int downstreamCount = DebuggerService.TraceDownstreamImpact(selectedRecord.NodeId, out _);

            if (upstream.FoundRootCause && upstream.TracePath != null)
            {
                CanvasRendererService.ActiveTracePath = upstream.TracePath;
                CanvasRendererService.RedrawCanvas();
            }

            string report = $"--- SMART DATA INSPECTOR ---\n" +
                            $"Target Node: {selectedRecord.NodeName}\n" +
                            $"Data Type: {health.DominantDataType}\n" +
                            $"Tree Depth: {health.MaxTreeDepth} lavel(s)\n" +
                            $"Total Items: {health.TotalItems}\n" +
                            $"Null Values: {health.NullCount}\n\n" +
                            $"--- UPSTREAM TRACE ---\n";

            if (upstream.FoundRootCause) report += $"⚠️ Root Cause: '{upstream.RootCauseNodeName}' ({upstream.StepsUpstream} steps upstream)\n👉 Error flow visually highlighted on canvas!\n";
            else report += $"✅ No upstream data corruption.\n";

            report += $"\n--- DOWNSTREAM IMPACT ---\n🔗 Directly/indirectly affects {downstreamCount} downstream node(s).\n";

            MessageBox.Show(this, report, $"GHX Debugger: {selectedRecord.NodeName}", MessageBoxButtons.OK);
        };
        return btn;
    }

    private Button CreateRefreshButton()
    {
        var btn = new Button { Text = "Refresh Metrics (Clear Trace)" };
        btn.Click += (s, e) => 
        {
            CanvasRendererService.ActiveTracePath.Clear();
            CanvasRendererService.RedrawCanvas();
            RefreshData();
        };
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
        var btn = new Button { Text = "Export Report" };
        btn.Click += (s, e) => 
        {
            var dialog = new SaveFileDialog { Title = "Export GHX Report", Filters = { new FileFilter("JSON Data", ".json"), new FileFilter("HTML Report", ".html"), new FileFilter("CSV Data", ".csv") } };
            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                bool success = false;
                if (dialog.FileName.EndsWith(".json")) success = ExportService.ExportToJson(dialog.FileName);
                else if (dialog.FileName.EndsWith(".html")) success = ExportService.ExportToHtml(dialog.FileName);
                else success = ExportService.ExportToCsv(dialog.FileName);
                
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