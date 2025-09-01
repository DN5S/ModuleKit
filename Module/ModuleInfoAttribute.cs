using System;

namespace TLink.Core.Module;

/// <summary>
/// Attribute to provide metadata about a module for automatic discovery and registration
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ModuleInfoAttribute(string name, string version) : Attribute
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public string Version { get; } = version ?? throw new ArgumentNullException(nameof(version));
    public string[] Dependencies { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool EnabledByDefault { get; set; } = true;
    public int Priority { get; set; } // Lower numbers load first
}
