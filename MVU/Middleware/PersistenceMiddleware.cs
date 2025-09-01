using System;
using System.Threading;
using System.Threading.Tasks;
using ModuleKit.Configuration;

namespace ModuleKit.MVU.Middleware;

public class PersistenceMiddleware<TState>(
    PluginConfiguration configuration,
    int debounceMilliseconds = 500)
    : IMiddleware<TState>, IDisposable
    where TState : IState
{
    private readonly PluginConfiguration configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly int debounceMilliseconds = Math.Max(100, debounceMilliseconds); // Minimum 100ms
    private long lastSavedVersion;
    private CancellationTokenSource? debounceCts;
    private readonly SemaphoreSlim saveSemaphore = new(1, 1);

    public async Task ProcessAsync(TState state, IAction action, Func<Task> next)
    {
        await next();
        
        // Only persist if the state version changed
        if (state.Version > lastSavedVersion)
        {
            _ = DebouncedSave(state.Version);
        }
    }
    
    private async Task DebouncedSave(long version)
    {
        // Cancel previous debounce timer
        debounceCts?.Cancel();
        debounceCts?.Dispose();
        
        // Create a new cancellation token
        debounceCts = new CancellationTokenSource();
        var token = debounceCts.Token;
        
        try
        {
            // Wait for the debounced period
            await Task.Delay(debounceMilliseconds, token);
            
            // Perform save if not canceled
            await saveSemaphore.WaitAsync(token);
            try
            {
                if (version > lastSavedVersion)
                {
                    configuration.Save();
                    lastSavedVersion = version;
                }
            }
            finally
            {
                saveSemaphore.Release();
            }
        }
        catch (TaskCanceledException)
        {
            // Normal - another save was triggered
        }
    }
    
    public async Task ForceSaveAsync()
    {
        debounceCts?.Cancel();
        
        await saveSemaphore.WaitAsync();
        try
        {
            configuration.Save();
        }
        finally
        {
            saveSemaphore.Release();
        }
    }
    
    public void Dispose()
    {
        debounceCts?.Cancel();
        debounceCts?.Dispose();
        saveSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
