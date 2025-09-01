using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace TLink.Core.Configuration;

[Serializable]
public class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    
    // Store configurations as JSON string for proper serialization
    public string ModuleConfigsJson { get; set; } = "{}";
    
    // Runtime-only container for actual configuration objects
    [JsonIgnore]
    internal Dictionary<string, ModuleConfiguration> ModuleConfigs { get; private set; } = new();
    
    [JsonIgnore]
    private IDalamudPluginInterface? pluginInterface;
    
    [JsonIgnore]
    private IJsonTypeInfoResolver? typeResolver;
    
    public void Initialize(IDalamudPluginInterface dalamudPluginInterface, IJsonTypeInfoResolver resolver)
    {
        this.pluginInterface = dalamudPluginInterface;
        this.typeResolver = resolver;
        Load();
    }
    
    public void Save()
    {
        if (typeResolver != null)
        {
            var options = new JsonSerializerOptions 
            { 
                TypeInfoResolver = typeResolver,
                WriteIndented = true 
            };
            ModuleConfigsJson = JsonSerializer.Serialize(ModuleConfigs, options);
        }
        pluginInterface?.SavePluginConfig(this);
    }
    
    public void Load()
    {
        var config = pluginInterface?.GetPluginConfig();
        if (config is PluginConfiguration pluginConfig && !string.IsNullOrEmpty(pluginConfig.ModuleConfigsJson))
        {
            Version = pluginConfig.Version;
            if (typeResolver != null)
            {
                var options = new JsonSerializerOptions { TypeInfoResolver = typeResolver };
                try
                {
                    ModuleConfigs = JsonSerializer.Deserialize<Dictionary<string, ModuleConfiguration>>(
                        pluginConfig.ModuleConfigsJson, options) ?? new Dictionary<string, ModuleConfiguration>();
                }
                catch
                {
                    ModuleConfigs = new Dictionary<string, ModuleConfiguration>();
                }
            }
        }
    }
    
    public void Reset()
    {
        ModuleConfigs.Clear();
        ModuleConfigsJson = "{}";
        Save();
    }
    
    // Module-specific configuration helpers
    public T GetModuleConfig<T>(string moduleName) where T : ModuleConfiguration, new()
    {
        var key = $"Module.{moduleName}";
        if (ModuleConfigs.TryGetValue(key, out var config) && config is T typedConfig)
        {
            return typedConfig;
        }
        
        // Create a new config and add to dictionary to prevent orphaned objects
        var newConfig = new T { ModuleName = moduleName };
        ModuleConfigs[key] = newConfig;
        return newConfig;
    }
    
    // Non-generic overload for backward compatibility
    public ModuleConfiguration GetModuleConfig(string moduleName)
    {
        return GetModuleConfig<ModuleConfiguration>(moduleName);
    }
    
    public void SetModuleConfig(string moduleName, ModuleConfiguration config)
    {
        var key = $"Module.{moduleName}";
        ModuleConfigs[key] = config;
    }
    
    /// <summary>
    /// Gets all module configurations
    /// </summary>
    public Dictionary<string, ModuleConfiguration> GetAllModuleConfigs()
    {
        var configs = new Dictionary<string, ModuleConfiguration>();
        foreach (var kvp in ModuleConfigs)
        {
            if (kvp.Key.StartsWith("Module."))
            {
                var moduleName = kvp.Key[7..]; // Remove "Module." prefix
                configs[moduleName] = kvp.Value;
            }
        }
        return configs;
    }
    
    /// <summary>
    /// Removes a module configuration
    /// </summary>
    public void RemoveModuleConfig(string moduleName)
    {
        var key = $"Module.{moduleName}";
        ModuleConfigs.Remove(key);
    }
}

// Pure base class for module configurations
public class ModuleConfiguration
{
    public string ModuleName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
