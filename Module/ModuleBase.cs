using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using ModuleKit.Configuration;
using ModuleKit.Reactive;

namespace ModuleKit.Module;

public abstract class ModuleBase : IModule
{
    protected readonly CompositeDisposable Subscriptions = new();
    protected IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    protected IPluginLog Logger { get; private set; } = null!;
    protected EventBus EventBus { get; private set; } = null!;
    protected IServiceProvider Services { get; private set; } = null!;
    protected PluginConfiguration Configuration { get; private set; } = null!;
    
    public abstract string Name { get; }
    public abstract string Version { get; }
    public virtual string[] Dependencies => [];
    public virtual Type? ConfigurationType => null;
    
    public abstract void RegisterServices(IServiceCollection services);

    public virtual void RegisterSharedServices(IServiceCollection services)
    {
    }

    public virtual void Initialize()
    {
    }
    
    public virtual async Task InitializeAsync()
    {
        Initialize();
        await Task.CompletedTask;
    }
    
    public virtual void DrawUI()
    {
    }
    
    public virtual void DrawConfiguration()
    {
    }
    
    public void InjectDependencies(IServiceProvider services)
    {
        Services = services;
        PluginInterface = services.GetRequiredService<IDalamudPluginInterface>();
        Logger = services.GetRequiredService<IPluginLog>();
        EventBus = services.GetRequiredService<EventBus>();
        Configuration = services.GetRequiredService<PluginConfiguration>();
        
        // Load module configuration
        LoadConfiguration();
    }
    
    protected virtual void LoadConfiguration()
    {
        // Override in derived classes to load module-specific configuration
    }
    
    protected virtual void SaveConfiguration()
    {
        Configuration.Save();
    }
    
    protected T GetModuleConfig<T>() where T : ModuleConfiguration, new()
    {
        var config = Configuration.GetModuleConfig(Name);
        if (config is T typedConfig)
            return typedConfig;
        
        // Create the default config if not found or wrong type
        var newConfig = new T { ModuleName = Name };
        SetModuleConfig(newConfig);
        return newConfig;
    }
    
    protected void SetModuleConfig<T>(T config) where T : ModuleConfiguration
    {
        Configuration.SetModuleConfig(Name, config);
        SaveConfiguration();
    }
    
    public virtual void Dispose()
    {
        Subscriptions.Dispose();
        GC.SuppressFinalize(this);
    }
}
