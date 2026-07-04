using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GHXStudio.Core.Models;

namespace GHXStudio.Core.Services;

/// <summary>
/// The core Static Analysis & Linter engine.
/// Evaluates nodes against dynamic BIM rulesets and detects script-level memory vulnerabilities.
/// </summary>
public static class RuleEngineService
{
    // Baseline fallback ruleset (Default rules if no JSON is loaded)
    private static BimRuleset _activeRuleset = new BimRuleset(); 

    /// <summary>
    /// Injects a custom JSON ruleset into the Linter engine at runtime.
    /// Overrides the default ruleset.
    /// </summary>
    public static bool LoadCustomRules(string jsonPath)
    {
        try
        {
            string json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var loadedRules = JsonSerializer.Deserialize<BimRuleset>(json, options);
            
            if (loadedRules != null)
            {
                _activeRuleset = loadedRules; // Override defaults with uploaded rules
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Rhino.RhinoApp.WriteLine($"[GHX Linter] Failed to load custom ruleset: {ex.Message}");
            return false;
        }
    }

    public static string EvaluateNode(NodeTelemetryRecord node)
    {
        var warnings = new List<string>();

        // 1. Performance Heuristics
        if (node.ExecutionTimeMs > _activeRuleset.MaxExecutionTimeMs)
            warnings.Add($"[Rule 1] Suboptimal Time: {node.ExecutionTimeMs:F1}ms (Limit: {_activeRuleset.MaxExecutionTimeMs}ms)");

        // 2. Memory Footprint Heuristics
        if (node.MemoryEstimateMb > _activeRuleset.MaxMemoryMb && node.MemoryEstimateMb <= _activeRuleset.SevereMemoryMb)
            warnings.Add($"[Rule 2] High Memory Footprint: {node.MemoryEstimateMb:F2}MB");
            
        // NEW CUTTING-EDGE RULE: Detect massive volatile memory hogs
        if (node.MemoryEstimateMb > _activeRuleset.SevereMemoryMb)
            warnings.Add($"[Rule 4] SEVERE MEMORY RISK: Generating massive volatile data ({node.MemoryEstimateMb:F1}MB)");

        // 3. Native Runtime Execution Errors
        if (node.ErrorCount > 0)
            warnings.Add("[Rule 3] Critical: Native runtime failure detected.");

        // 4. Enterprise BIM Standardization (Forbidden Nodes)
        if (_activeRuleset.ForbiddenNodes != null && 
           (_activeRuleset.ForbiddenNodes.Contains(node.NodeType) || _activeRuleset.ForbiddenNodes.Contains(node.NodeName)))
            warnings.Add($"[Rule 5] FORBIDDEN: Node '{node.NodeType}' violates active BIM strict standards.");

        // 5. Unmanaged Script Profiling (C# / Python GC Leak Risks)
        if (_activeRuleset.FlagCustomScripts && IsCustomScript(node.NodeType))
        {
            warnings.Add("[Rule 6] SECURITY/LEAK RISK: Unmanaged custom script detected. Check for undisposed geometry/memory.");
        }

        return warnings.Count > 0 ? string.Join(" | ", warnings) : "Optimal";
    }

    /// <summary>
    /// Heuristic pattern matching to identify user-created scripts.
    /// </summary>
    private static bool IsCustomScript(string nodeType)
    {
        string typeUpper = nodeType.ToUpperInvariant();
        return typeUpper.Contains("PYTHON") || typeUpper.Contains("C#") || typeUpper.Contains("SCRIPT");
    }
}