using System;
using Microsoft.Extensions.DependencyInjection;

namespace TLink.Core.Module;

public interface IModule : IDisposable
{
    string Name { get; }
    string Version { get; }
    string[] Dependencies { get; }
    
    void RegisterServices(IServiceCollection services);
    void RegisterSharedServices(IServiceCollection services);
    void Initialize();
    void DrawUI();
    void DrawConfiguration();
}