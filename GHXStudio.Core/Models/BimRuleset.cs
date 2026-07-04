using System;
using System.Collections.Generic;

namespace GHXStudio.Core.Models;

/// <summary>
/// Represents a customizable JSON ruleset defined by BIM Managers 
/// to enforce strict graph performance and architecture standards.
/// </summary>
public class BimRuleset
{
    public double MaxExecutionTimeMs { get; set; } = 20.0;
    public double MaxMemoryMb { get; set; } = 5.0;
    public double SevereMemoryMb { get; set; } = 50.0;
    public List<string> ForbiddenNodes { get; set; } = new List<string>();
    public bool FlagCustomScripts { get; set; } = true;
}