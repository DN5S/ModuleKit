using System;
using System.Reflection;

namespace ModuleKit.Module;

public class ModuleInstance
{
    public IModule Module { get; }
    public ModuleStatus Status { get; private set; }
    public Exception? Exception { get; private set; }
    public DateTime LoadedAt { get; }
    public DateTime? FailedAt { get; private set; }
    public IServiceProvider? ServiceProvider { get; set; }
    public ModuleInfoAttribute? Metadata { get; set; }
    public int InitializationAttempts { get; private set; }
    public bool IsEnabled { get; private set; }
    
    public bool IsHealthy => Status == ModuleStatus.Running;
    public bool IsUsable => Status is ModuleStatus.Running or ModuleStatus.Initializing;
    
    public ModuleInstance(IModule module)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Status = ModuleStatus.Uninitialized;
        LoadedAt = DateTime.UtcNow;
        Metadata = module.GetType().GetCustomAttribute<ModuleInfoAttribute>(inherit: false);
        IsEnabled = Metadata?.EnabledByDefault ?? true;

    }
    
    public void MarkAsInitializing()
    {
        if (Status == ModuleStatus.Disposed)
            throw new InvalidOperationException("Cannot initialize a disposed module.");
        if (!IsEnabled)
            throw new InvalidOperationException("Cannot initialize a disabled module.");
        
        Status = ModuleStatus.Initializing;
        InitializationAttempts++;
    }
    
    public void MarkAsRunning()
    {
        if (Status != ModuleStatus.Initializing)
            throw new InvalidOperationException($"Module must be in Initializing state to mark as Running. Current state: {Status}");
        
        Status = ModuleStatus.Running;
        Exception = null;
        FailedAt = null;
    }
    
    public void MarkAsFailed(Exception exception)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Status = ModuleStatus.Failed;
        FailedAt = DateTime.UtcNow;
    }
    
    public void MarkAsDisposed()
    {
        Status = ModuleStatus.Disposed;
        
        if (Module is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Exception = ex;
                FailedAt = DateTime.UtcNow;
            }
        }
        
        if (ServiceProvider is IDisposable disposableProvider)
        {
            try
            {
                disposableProvider.Dispose();
            }
            catch
            {
                // Swallow disposal exceptions for the service provider
            }
        }
    }
    
    public bool TryRecover()
    {
        switch (Status)
        {
            case ModuleStatus.Disposed:
                return false;
            case ModuleStatus.Failed:
                Status = ModuleStatus.Uninitialized;
                Exception = null;
                FailedAt = null;
                return true;
            case ModuleStatus.Uninitialized:
            case ModuleStatus.Initializing:
            case ModuleStatus.Running:
            default:
                return false;
        }
    }
    
    // User can enable/disable modules via the UI
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }
    
    public override string ToString()
    {
        var name = Metadata?.Name ?? Module.Name;
        var version = Metadata?.Version ?? Module.Version;
        var result = $"Module: {name} v{version} - Status: {Status} - Enabled: {IsEnabled}";
        
        if (Exception != null)
        {
            result += $" - Error: {Exception.Message}";
        }
        
        if (InitializationAttempts > 1)
        {
            result += $" - Attempts: {InitializationAttempts}";
        }
        
        return result;
    }
}
