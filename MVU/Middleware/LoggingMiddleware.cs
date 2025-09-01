using System;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace ModuleKit.MVU.Middleware;

public class LoggingMiddleware<TState>(IPluginLog logger) : IMiddleware<TState>
    where TState : IState
{
    public async Task ProcessAsync(TState state, IAction action, Func<Task> next)
    {
        var actionType = action.GetType().Name;
        var stateVersion = state.Version;
        
        logger.Debug($"[MVU] Dispatching action: {actionType} (State v{stateVersion})");
        
        try
        {
            await next();
            logger.Debug($"[MVU] Action completed: {actionType}");
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"[MVU] Action failed: {actionType}");
            throw;
        }
    }
}