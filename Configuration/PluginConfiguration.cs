using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Configuration;
using Dalamud.Plugin;
using ModuleKit.Module;

namespace ModuleKit.Configuration;

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
    private ModuleConfigurationJsonConverter? jsonConverter;
    
    [JsonIgnore]
    private JsonSerializerOptions? serializerOptions;
    
    public void Initialize(IDalamudPluginInterface dalamudPluginInterface, ModuleRegistry? registry = null)
    {
        pluginInterface = dalamudPluginInterface;
        
        // Get safe configuration types from the registry if available
        var safeTypes = registry?.GetSafeConfigurationTypes() ?? new Dictionary<string, Type>();
        
        jsonConverter = new ModuleConfigurationJsonConverter(safeTypes);
        serializerOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Converters = { jsonConverter }
        };
        
        Load();
    }
    
    public void Save()
    {
        if (serializerOptions != null)
        {
            ModuleConfigsJson = JsonSerializer.Serialize(ModuleConfigs, serializerOptions);
        }
        pluginInterface?.SavePluginConfig(this);
    }
    
    public void Load()
    {
        var config = pluginInterface?.GetPluginConfig();
        if (config is PluginConfiguration pluginConfig && !string.IsNullOrEmpty(pluginConfig.ModuleConfigsJson))
        {
            Version = pluginConfig.Version;
            if (serializerOptions != null)
            {
                try
                {
                    ModuleConfigs = JsonSerializer.Deserialize<Dictionary<string, ModuleConfiguration>>(
                        pluginConfig.ModuleConfigsJson, serializerOptions) ?? new Dictionary<string, ModuleConfiguration>();
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
            // Check if migration is needed
            var latestVersion = new T().ConfigVersion;
            if (typedConfig.ConfigVersion < latestVersion)
            {
                typedConfig.Migrate(typedConfig.ConfigVersion, latestVersion);
                typedConfig.ConfigVersion = latestVersion;
                Save();
            }
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
public class ModuleConfiguration : IMigratableConfiguration
{
    public string ModuleName { get; init; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int ConfigVersion { get; set; } = 1;
    
    public virtual void Migrate(int fromVersion, int toVersion)
    {
        // Override in derived classes to handle configuration migrations
    }
}
