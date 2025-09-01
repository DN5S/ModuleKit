using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TLink.Core.Configuration;
using TLink.Core.Reactive;
using TLink.Utils;

namespace TLink.Core.Module;

/// <summary>
/// Extension methods for consistent service registration across modules
/// </summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Registers all core Dalamud services from the global service provider
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IServiceProvider globalServices)
    {
        ArgumentNullException.ThrowIfNull(globalServices);
        ArgumentNullException.ThrowIfNull(services);
        
        // Register the global service provider itself
        services.AddSingleton(globalServices);
        
        // Register required core services
        services.AddSingleton(globalServices.GetRequiredService<IPluginLog>());
        services.AddSingleton(globalServices.GetRequiredService<EventBus>());
        services.AddSingleton(globalServices.GetRequiredService<PluginConfiguration>());
        services.AddSingleton(globalServices.GetRequiredService<ModuleManager>());
        services.AddSingleton(globalServices.GetRequiredService<SeStringProcessor>());
        
        return services;
    }
    
    /// <summary>
    /// Registers optional Dalamud services if they exist in the global service provider
    /// </summary>
    public static IServiceCollection AddOptionalDalamudServices(this IServiceCollection services, IServiceProvider globalServices)
    {
        ArgumentNullException.ThrowIfNull(globalServices);
        ArgumentNullException.ThrowIfNull(services);
        
        // Register optional services only if they exist
        services.AddSingletonIfExists<IDalamudPluginInterface>(globalServices);
        services.AddSingletonIfExists<ICommandManager>(globalServices);
        services.AddSingletonIfExists<IChatGui>(globalServices);
        services.AddSingletonIfExists<WindowSystem>(globalServices);
        services.AddSingletonIfExists<IClientState>(globalServices);
        services.AddSingletonIfExists<IDataManager>(globalServices);
        services.AddSingletonIfExists<IFramework>(globalServices);
        services.AddSingletonIfExists<IGameGui>(globalServices);
        services.AddSingletonIfExists<ICondition>(globalServices);
        
        return services;
    }
    
    /// <summary>
    /// Registers all standard services for a module
    /// </summary>
    public static IServiceCollection AddModuleServices(this IServiceCollection services, IServiceProvider globalServices)
    {
        return services
            .AddCoreServices(globalServices)
            .AddOptionalDalamudServices(globalServices);
    }
    
    /// <summary>
    /// Helper method to add a singleton service only if it exists in the source provider
    /// </summary>
    // ReSharper disable once UnusedMethodReturnValue.Local
    private static IServiceCollection AddSingletonIfExists<T>(this IServiceCollection services, IServiceProvider provider) 
        where T : class
    {
        var service = provider.GetService<T>();
        if (service != null)
        {
            services.AddSingleton(service);
        }
        
        return services;
    }
}
