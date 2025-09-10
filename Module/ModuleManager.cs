using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModuleKit.Configuration;

namespace ModuleKit.Module;

public class ModuleManager(IServiceProvider globalServices, IPluginLog logger) : IDisposable
{
    private readonly List<ModuleInstance> moduleInstances = [];
    private readonly ServiceCollection sharedServices = new();
    private ModuleRegistry? registry;
    
    public IReadOnlyList<IModule> LoadedModules => moduleInstances
        .Where(mi => mi.IsHealthy)
        .Select(mi => mi.Module)
        .ToList()
        .AsReadOnly();
    
    public IReadOnlyList<ModuleInstance> AllModuleInstances => moduleInstances.AsReadOnly();
    public ModuleRegistry Registry => registry ??= new ModuleRegistry(logger);

    public void LoadModule<T>() where T : IModule, new()
    {
        var module = new T();
        _ = LoadModule(module);
    }
    
    public async Task LoadModuleAsync<T>() where T : IModule, new()
    {
        var module = new T();
        await LoadModuleAsync(module);
    }
    
    public async Task LoadModule(IModule module)
    {
        await LoadModuleAsync(module).ConfigureAwait(false);
    }
    
    public async Task LoadModuleAsync(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        
        var moduleInstance = new ModuleInstance(module);
        
        if (moduleInstances.Any(mi => mi.Module.Name == module.Name))
        {
            logger.Warning($"Module {module.Name} is already loaded");
            return;
        }
        
        // Track services added during this module's initialization for potential rollback
        var sharedServicesSnapshot = new ServiceCollection();
        foreach (var descriptor in sharedServices)
        {
            sharedServicesSnapshot.Add(descriptor);
        }
        
        ServiceProvider? moduleProvider = null;
        var rollbackRequired = false;
        
        try
        {
            moduleInstance.MarkAsInitializing();
            
            // Validate dependencies
            foreach (var dependency in module.Dependencies)
            {
                var depInstance = moduleInstances.FirstOrDefault(mi => mi.Module.Name == dependency);
                if (depInstance is not { IsHealthy: true })
                {
                    throw new InvalidOperationException($"Module {module.Name} requires {dependency} which is not loaded or healthy");
                }
            }
            
            // Register shared services
            module.RegisterSharedServices(sharedServices);
            rollbackRequired = true; // Mark that we need rollback if failure occurs after this point
            
            // Create module-specific service collection
            var services = new ServiceCollection();
            services.AddModuleServices(globalServices);
            
            foreach (var descriptor in sharedServices)
            {
                services.TryAdd(descriptor);
            }
            
            module.RegisterServices(services);
            
            // Build and store service provider
            moduleProvider = services.BuildServiceProvider();
            moduleInstance.ServiceProvider = moduleProvider;
            
            if (module is ModuleBase moduleBase)
            {
                moduleBase.InjectDependencies(moduleProvider);
            }
            
            // Initialize with async support
            await module.InitializeAsync().ConfigureAwait(false);
            
            moduleInstance.MarkAsRunning();
            moduleInstances.Add(moduleInstance);
            
            logger.Information($"Loaded module: {module.Name} v{module.Version}");
            rollbackRequired = false; // Success, no rollback needed
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Failed to load module: {module.Name}. Initiating cleanup.");
            
            // Perform cleanup/rollback
            if (rollbackRequired)
            {
                try
                {
                    // Rollback shared services to the snapshot state
                    sharedServices.Clear();
                    foreach (var descriptor in sharedServicesSnapshot)
                    {
                        sharedServices.Add(descriptor);
                    }
                    
                    logger.Information($"Rolled back shared services for failed module: {module.Name}");
                }
                catch (Exception rollbackEx)
                {
                    logger.Error(rollbackEx, $"Failed to rollback shared services for module: {module.Name}");
                }
            }
            
            // Dispose the service provider if it was created
            if (moduleProvider != null)
            {
                try
                {
                    if (moduleProvider is IDisposable disposableProvider)
                    {
                        disposableProvider.Dispose();
                    }
                }
                catch (Exception disposeEx)
                {
                    logger.Error(disposeEx, $"Failed to dispose service provider for module: {module.Name}");
                }
            }
            
            // Try to clean up the module if it partially initialized
            if (module is IDisposable disposableModule)
            {
                try
                {
                    disposableModule.Dispose();
                }
                catch (Exception moduleDisposeEx)
                {
                    logger.Error(moduleDisposeEx, $"Failed to dispose module: {module.Name}");
                }
            }
            
            moduleInstance.MarkAsFailed(ex);
            moduleInstances.Add(moduleInstance);
            
            logger.Error(ex, $"Module {module.Name} marked as failed after cleanup.");
        }
    }
    
    public void UnloadModule(string moduleName)
    {
        var moduleInstance = moduleInstances.FirstOrDefault(mi => mi.Module.Name == moduleName);
        if (moduleInstance == null) return;
        
        var dependents = moduleInstances
            .Where(mi => mi.Module.Dependencies.Contains(moduleName))
            .ToList();
        
        foreach (var dependent in dependents)
        {
            UnloadModule(dependent.Module.Name);
        }
        
        moduleInstance.MarkAsDisposed();
        moduleInstances.Remove(moduleInstance);
        
        logger.Information($"Unloaded module: {moduleName}");
    }
    
    public void DrawUI()
    {
        foreach (var instance in moduleInstances.Where(mi => mi.IsHealthy))
        {
            try
            {
                instance.Module.DrawUI();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error drawing UI for module: {instance.Module.Name}");
                instance.MarkAsFailed(ex);
            }
        }
    }
    
    public void DrawConfiguration()
    {
        foreach (var instance in moduleInstances.Where(mi => mi.IsHealthy))
        {
            try
            {
                instance.Module.DrawConfiguration();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error drawing configuration for module: {instance.Module.Name}");
                instance.MarkAsFailed(ex);
            }
        }
    }
    
    /// <summary>
    /// Discovers and loads all registered modules
    /// </summary>
    public async Task LoadAllRegisteredModulesAsync(PluginConfiguration configuration)
    {
        Registry.DiscoverModules();
        
        if (!Registry.ValidateDependencies())
        {
            logger.Warning("Some module dependencies are not satisfied");
        }
        
        var modulesToLoad = Registry.GetModulesInLoadOrder();
        
        // Load modules sequentially to respect dependencies
        foreach (var moduleName in modulesToLoad)
        {
            var moduleConfig = configuration.GetModuleConfig(moduleName);
            
            if (!moduleConfig.IsEnabled)
            {
                logger.Information($"Skipping disabled module: {moduleName}");
                continue;
            }
            
            var module = Registry.CreateModuleInstance(moduleName);
            if (module != null)
            {
                await LoadModuleAsync(module).ConfigureAwait(false);
            }
        }
    }
    
    public async Task LoadAllRegisteredModules(PluginConfiguration configuration)
    {
        await LoadAllRegisteredModulesAsync(configuration).ConfigureAwait(false);
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
        return moduleInstances
            .FirstOrDefault(mi => mi.Module.Name == moduleName && mi.IsHealthy)?
            .Module;
    }
    
    /// <summary>
    /// Gets a module instance wrapper by name, including failed modules
    /// </summary>
    public ModuleInstance? GetModuleInstance(string moduleName)
    {
        return moduleInstances.FirstOrDefault(mi => mi.Module.Name == moduleName);
    }
    
    /// <summary>
    /// Gets all modules that directly depend on the specified module
    /// </summary>
    public IReadOnlyList<string> GetDependentModules(string moduleName)
    {
        var dependents = new List<string>();
        
        foreach (var instance in moduleInstances)
        {
            if (instance.Module.Dependencies.Contains(moduleName))
            {
                dependents.Add(instance.Module.Name);
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
    public async Task ApplyConfigurationChangesAsync(PluginConfiguration configuration)
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
            
            var isCurrentlyLoaded = moduleInstances.Any(mi => mi.Module.Name == moduleName && mi.IsHealthy);
            
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
                    var depInstance = moduleInstances.FirstOrDefault(mi => mi.Module.Name == dep);
                    if (!depConfig.IsEnabled || depInstance is not { IsHealthy: true })
                    {
                        logger.Warning($"Cannot load module {moduleName} because dependency {dep} is not enabled or healthy");
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
                            await LoadModuleAsync(module).ConfigureAwait(false);
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
    
    /// <summary>
    /// Attempts to recover failed modules by reinitializing them
    /// </summary>
    public async Task RecoverFailedModulesAsync()
    {
        var failedModules = moduleInstances
            .Where(mi => mi is { Status: ModuleStatus.Failed, InitializationAttempts: < 3 })
            .ToList();
        
        foreach (var instance in failedModules)
        {
            logger.Information($"Attempting to recover module: {instance.Module.Name}");
            
            if (instance.TryRecover())
            {
                moduleInstances.Remove(instance);
                await LoadModuleAsync(instance.Module).ConfigureAwait(false);
            }
        }
    }
    
    public async Task RecoverFailedModules()
    {
        await RecoverFailedModulesAsync().ConfigureAwait(false);
    }
    
    /// <summary>
    /// Gets diagnostic information about all modules
    /// </summary>
    public Dictionary<string, ModuleStatus> GetModuleStatuses()
    {
        return moduleInstances.ToDictionary(
            mi => mi.Module.Name,
            mi => mi.Status
        );
    }
    
    public void Dispose()
    {
        foreach (var instance in moduleInstances.ToList())
        {
            UnloadModule(instance.Module.Name);
        }
        GC.SuppressFinalize(this);   
    }
}
