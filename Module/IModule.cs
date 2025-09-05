using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ModuleKit.Module;

public interface IModule : IDisposable
{
    string Name { get; }
    string Version { get; }
    string[] Dependencies { get; }
    Type? ConfigurationType { get; }
    
    void RegisterServices(IServiceCollection services);
    void RegisterSharedServices(IServiceCollection services);
    void Initialize();
    Task InitializeAsync();
    void DrawUI();
    void DrawConfiguration();
}
