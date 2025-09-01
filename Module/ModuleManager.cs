using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TLink.Core.Configuration;

namespace TLink.Core.Module;

public class ModuleManager(IServiceProvider globalServices, IPluginLog logger) : IDisposable
{
    private readonly List<IModule> modules = [];
    private readonly Dictionary<string, IServiceProvider> moduleServices = new();
    private readonly ServiceCollection sharedServices = new();
    private ModuleRegistry? registry;
    
    public IReadOnlyList<IModule> LoadedModules => modules.AsReadOnly();
    public ModuleRegistry Registry => registry ??= new ModuleRegistry(logger);

    public void LoadModule<T>() where T : IModule, new()
    {
        var module = new T();
        LoadModule(module);
    }
    
    public void LoadModule(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        try
        {
            if (modules.Any(m => m.Name == module.Name))
            {
                logger.Warning($"Module {module.Name} is already loaded");
                return;
            }
            
            foreach (var dependency in module.Dependencies)
            {
                if (modules.All(m => m.Name != dependency))
                {
                    logger.Error($"Module {module.Name} requires {dependency} which is not loaded");
                    throw new InvalidOperationException($"Module {module.Name} requires {dependency} which is not loaded");
                }
            }
            
            // Step 1: Register shared services from this module to the central collection
            module.RegisterSharedServices(sharedServices);
            
            // Step 2: Create a service collection for this module
            var services = new ServiceCollection();
            
            // Step 3: Add all standard services
            services.AddModuleServices(globalServices);
            
            // Step 4: Add all shared services from previously loaded modules
            foreach (var descriptor in sharedServices)
            {
                services.TryAdd(descriptor);
            }
            
            // Step 5: Let the module register its own specific services
            module.RegisterServices(services);
            
            // Step 6: Build the service provider
            var moduleProvider = services.BuildServiceProvider();
            moduleServices[module.Name] = moduleProvider;
            
            if (module is ModuleBase moduleBase)
            {
                moduleBase.InjectDependencies(moduleProvider);
            }
            
            module.Initialize();
            modules.Add(module);
            
            logger.Information($"Loaded module: {module.Name} v{module.Version}");
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Failed to load module: {module.Name}");
            throw;
        }
    }
    
    public void UnloadModule(string moduleName)
    {
        var module = modules.FirstOrDefault(m => m.Name == moduleName);
        if (module == null) return;
        
        var dependents = modules.Where(m => m.Dependencies.Contains(moduleName)).ToList();
        foreach (var dependent in dependents)
        {
            UnloadModule(dependent.Name);
        }
        
        module.Dispose();
        modules.Remove(module);
        
        if (moduleServices.TryGetValue(moduleName, out var provider))
        {
            if (provider is IDisposable disposable)
                disposable.Dispose();
            moduleServices.Remove(moduleName);
        }
        
        logger.Information($"Unloaded module: {moduleName}");
    }
    
    public void DrawUI()
    {
        foreach (var module in modules)
        {
            try
            {
                module.DrawUI();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error drawing UI for module: {module.Name}");
            }
        }
    }
    
    public void DrawConfiguration()
    {
        foreach (var module in modules)
        {
            try
            {
                module.DrawConfiguration();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error drawing configuration for module: {module.Name}");
            }
        }
    }
    
    /// <summary>
    /// Discovers and loads all registered modules
    /// </summary>
    public void LoadAllRegisteredModules(PluginConfiguration configuration)
    {
        Registry.DiscoverModules();
        
        if (!Registry.ValidateDependencies())
        {
            logger.Warning("Some module dependencies are not satisfied");
        }
        
        var modulesToLoad = Registry.GetModulesInLoadOrder();
        
        foreach (var moduleName in modulesToLoad)
        {
            var moduleConfig = configuration.GetModuleConfig(moduleName);
            
            // Check if the module should be loaded based on configuration
            if (!moduleConfig.IsEnabled)
            {
                logger.Information($"Skipping disabled module: {moduleName}");
                continue;
            }
            
            var module = Registry.CreateModuleInstance(moduleName);
            if (module != null)
            {
                try
                {
                    LoadModule(module);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Failed to load module: {moduleName}");
                }
            }
        }
    }
    
    /// <summary>
    /// Gets module info for a loaded module
    /// </summary>
    public ModuleInfoAttribute? GetModuleInfo(string moduleName)
    {
        return Registry.ModuleInfos.GetValueOrDefault(moduleName);
    }
    
    /// <summary>
    /// Gets a loaded module instance by name
    /// </summary>
    public IModule? GetModule(string moduleName)
    {
        return modules.FirstOrDefault(m => m.Name == moduleName);
    }
    
    /// <summary>
    /// Gets all modules that directly depend on the specified module
    /// </summary>
    public IReadOnlyList<string> GetDependentModules(string moduleName)
    {
        var dependents = new List<string>();
        
        // Check loaded modules
        foreach (var module in modules)
        {
            if (module.Dependencies.Contains(moduleName))
            {
                dependents.Add(module.Name);
            }
        }
        
        // Also check registered but unloaded modules
        foreach (var kvp in Registry.ModuleInfos)
        {
            if (kvp.Value.Dependencies.Contains(moduleName) && !dependents.Contains(kvp.Key))
            {
                dependents.Add(kvp.Key);
            }
        }
        
        return dependents.AsReadOnly();
    }
    
    /// <summary>
    /// Gets all modules that depend on the specified module (including transitive dependencies)
    /// </summary>
    public IReadOnlyList<string> GetAllDependentModules(string moduleName)
    {
        var allDependents = new HashSet<string>();
        var toCheck = new Queue<string>();
        toCheck.Enqueue(moduleName);
        
        while (toCheck.Count > 0)
        {
            var current = toCheck.Dequeue();
            var directDependents = GetDependentModules(current);
            
            foreach (var dependent in directDependents)
            {
                if (allDependents.Add(dependent))
                {
                    toCheck.Enqueue(dependent);
                }
            }
        }
        
        return allDependents.ToList().AsReadOnly();
    }
    
    /// <summary>
    /// Checks if a module can be disabled safely (or returns a list of enabled dependents)
    /// </summary>
    public (bool canDisable, IReadOnlyList<string> dependents) CanDisableModule(string moduleName, PluginConfiguration configuration)
    {
        var allDependents = GetAllDependentModules(moduleName);
        
        // Filter to only include enabled dependents that would be affected
        var enabledDependents = new List<string>();
        foreach (var dependent in allDependents)
        {
            var depConfig = configuration.GetModuleConfig(dependent);
            if (depConfig.IsEnabled)
            {
                enabledDependents.Add(dependent);
            }
        }
        
        return (enabledDependents.Count == 0, enabledDependents.AsReadOnly());
    }
    
    /// <summary>
    /// Checks if a module can be disabled safely (or returns a list of dependents)
    /// This overload checks all dependents regardless of enabled status
    /// </summary>
    public (bool canDisable, IReadOnlyList<string> dependents) CanDisableModule(string moduleName)
    {
        var dependents = GetAllDependentModules(moduleName);
        return (dependents.Count == 0, dependents);
    }
    
    /// <summary>
    /// Applies configuration changes by loading/unloading modules as needed
    /// </summary>
    public void ApplyConfigurationChanges(PluginConfiguration configuration)
    {
        // First, discover all modules if not already done
        if (Registry.ModuleInfos.Count == 0)
        {
            Registry.DiscoverModules();
        }
        
        // Build list of modules that should be loaded based on config
        var modulesToLoad = new HashSet<string>();
        var modulesToUnload = new HashSet<string>();
        
        foreach (var kvp in Registry.ModuleInfos)
        {
            var moduleName = kvp.Key;
            var moduleConfig = configuration.GetModuleConfig(moduleName);
            
            var isCurrentlyLoaded = modules.Any(m => m.Name == moduleName);
            
            switch (moduleConfig.IsEnabled)
            {
                case true when !isCurrentlyLoaded:
                    // Module should be loaded but isn't
                    modulesToLoad.Add(moduleName);
                    break;
                case false when isCurrentlyLoaded:
                    // Module is loaded but shouldn't be
                    modulesToUnload.Add(moduleName);
                    break;
            }
        }
        
        // Unload modules that should not be loaded
        foreach (var moduleName in modulesToUnload)
        {
            logger.Information($"Unloading module {moduleName} due to configuration change");
            UnloadModule(moduleName);
        }
        
        // Load modules that should be loaded (in dependency order)
        if (modulesToLoad.Count > 0)
        {
            var orderedModules = Registry.GetModulesInLoadOrder()
                .Where(m => modulesToLoad.Contains(m))
                .ToList();
            
            foreach (var moduleName in orderedModules)
            {
                // Check dependencies are satisfied
                var moduleInfo = Registry.ModuleInfos[moduleName];
                var dependenciesSatisfied = true;
                
                foreach (var dep in moduleInfo.Dependencies)
                {
                    var depConfig = configuration.GetModuleConfig(dep);
                    if (!depConfig.IsEnabled || modules.All(m => m.Name != dep))
                    {
                        logger.Warning($"Cannot load module {moduleName} because dependency {dep} is not enabled");
                        dependenciesSatisfied = false;
                        break;
                    }
                }
                
                if (dependenciesSatisfied)
                {
                    var module = Registry.CreateModuleInstance(moduleName);
                    if (module != null)
                    {
                        try
                        {
                            logger.Information($"Loading module {moduleName} due to configuration change");
                            LoadModule(module);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Failed to load module: {moduleName}");
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Checks if all dependencies for a module are satisfied
    /// </summary>
    public bool AreDependenciesSatisfied(string moduleName, PluginConfiguration configuration)
    {
        var moduleInfo = Registry.ModuleInfos.GetValueOrDefault(moduleName);
        if (moduleInfo == null) return false;
        
        foreach (var dep in moduleInfo.Dependencies)
        {
            var depConfig = configuration.GetModuleConfig(dep);
            if (!depConfig.IsEnabled)
            {
                return false;
            }
        }
        
        return true;
    }
    
    public void Dispose()
    {
        foreach (var module in modules.ToList())
        {
            UnloadModule(module.Name);
        }
        GC.SuppressFinalize(this);   
    }
}
