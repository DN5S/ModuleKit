using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin.Services;

namespace ModuleKit.Module;

/// <summary>
/// Registry for discovering and managing modules through reflection
/// </summary>
public class ModuleRegistry(IPluginLog logger)
{
    private readonly IPluginLog logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Dictionary<string, Type> registeredModules = new();
    private readonly Dictionary<string, ModuleInfoAttribute> moduleInfos = new();
    
    public IReadOnlyDictionary<string, Type> RegisteredModules => registeredModules;
    public IReadOnlyDictionary<string, ModuleInfoAttribute> ModuleInfos => moduleInfos;

    /// <summary>
    /// Discovers all modules in the current assembly that inherit from ModuleBase
    /// and have the ModuleInfo attribute
    /// </summary>
    public void DiscoverModules()
    {
        DiscoverModulesInAssembly(Assembly.GetExecutingAssembly());
    }
    
    /// <summary>
    /// Discovers modules in a specific assembly
    /// </summary>
    public void DiscoverModulesInAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            var moduleTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && 
                           t.IsSubclassOf(typeof(ModuleBase)) &&
                           t.GetCustomAttribute<ModuleInfoAttribute>() != null)
                .ToList();
            
            foreach (var moduleType in moduleTypes)
            {
                var moduleInfo = moduleType.GetCustomAttribute<ModuleInfoAttribute>();
                if (moduleInfo == null) continue;
                
                RegisterModule(moduleInfo.Name, moduleType, moduleInfo);
                logger.Information($"Discovered module: {moduleInfo.Name} v{moduleInfo.Version} [{moduleType.FullName}]");
            }
            
            logger.Information($"Discovered {moduleTypes.Count} modules in assembly {assembly.GetName().Name}");
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Failed to discover modules in assembly {assembly.GetName().Name}");
        }
    }
    
    /// <summary>
    /// Manually registers a module type
    /// </summary>
    public void RegisterModule(string name, Type moduleType, ModuleInfoAttribute? info = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        ArgumentNullException.ThrowIfNull(moduleType);

        if (!moduleType.IsSubclassOf(typeof(ModuleBase)))
            throw new ArgumentException($"Type {moduleType.Name} must inherit from ModuleBase");
        
        if (registeredModules.ContainsKey(name))
        {
            logger.Warning($"Module {name} is already registered, replacing with {moduleType.FullName}");
        }
        
        registeredModules[name] = moduleType;
        
        if (info != null)
        {
            moduleInfos[name] = info;
        }
        else
        {
            // Try to get info from the attribute if not provided
            var attrInfo = moduleType.GetCustomAttribute<ModuleInfoAttribute>();
            if (attrInfo != null)
            {
                moduleInfos[name] = attrInfo;
            }
        }
    }
    
    /// <summary>
    /// Unregisters a module
    /// </summary>
    public void UnregisterModule(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));
        
        if (registeredModules.Remove(name))
        {
            moduleInfos.Remove(name);
            logger.Information($"Unregistered module: {name}");
        }
    }
    
    /// <summary>
    /// Creates an instance of a registered module
    /// </summary>
    public IModule? CreateModuleInstance(string name)
    {
        if (!registeredModules.TryGetValue(name, out var moduleType))
        {
            logger.Warning($"Module {name} is not registered");
            return null;
        }
        
        try
        {
            var instance = Activator.CreateInstance(moduleType) as IModule;
            if (instance == null)
            {
                logger.Error($"Failed to create instance of module {name}");
                return null;
            }
            
            return instance;
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Failed to create instance of module {name}");
            return null;
        }
    }
    
    /// <summary>
    /// Gets all registered modules sorted by priority and dependencies
    /// </summary>
    public IEnumerable<string> GetModulesInLoadOrder()
    {
        var sorted = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        
        // First, sort by priority
        var modulesByPriority = moduleInfos
            .OrderBy(kvp => kvp.Value.Priority)
            .ThenBy(kvp => kvp.Key)
            .Select(kvp => kvp.Key)
            .ToList();
        
        // Then apply dependency ordering
        foreach (var moduleName in modulesByPriority)
        {
            VisitModule(moduleName, sorted, visited, visiting);
        }
        
        return sorted;
    }
    
    private void VisitModule(string moduleName, List<string> sorted, HashSet<string> visited, HashSet<string> visiting)
    {
        if (visited.Contains(moduleName))
            return;
        
        if (!visiting.Add(moduleName))
        {
            logger.Warning($"Circular dependency detected involving module: {moduleName}");
            return;
        }

        // Visit dependencies first
        if (moduleInfos.TryGetValue(moduleName, out var info))
        {
            foreach (var dependency in info.Dependencies)
            {
                if (registeredModules.ContainsKey(dependency))
                {
                    VisitModule(dependency, sorted, visited, visiting);
                }
            }
        }
        
        visiting.Remove(moduleName);
        visited.Add(moduleName);
        
        if (registeredModules.ContainsKey(moduleName))
        {
            sorted.Add(moduleName);
        }
    }
    
    /// <summary>
    /// Validates that all module dependencies are satisfied
    /// </summary>
    public bool ValidateDependencies()
    {
        var isValid = true;
        
        foreach (var (moduleName, info) in moduleInfos)
        {
            foreach (var dependency in info.Dependencies)
            {
                if (!registeredModules.ContainsKey(dependency))
                {
                    logger.Error($"Module {moduleName} has unsatisfied dependency: {dependency}");
                    isValid = false;
                }
            }
        }
        
        return isValid;
    }
}
