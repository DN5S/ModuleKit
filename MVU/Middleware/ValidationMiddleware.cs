using System;
using System.Threading.Tasks;

namespace ModuleKit.MVU.Middleware;

public class ValidationMiddleware<TState>(
    Func<TState, IAction, bool>? preValidation = null,
    Func<TState, bool>? postValidation = null)
    : IMiddleware<TState>
    where TState : IState
{
    public async Task ProcessAsync(TState state, IAction action, Func<Task> next)
    {
        // Validate action before processing
        if (preValidation != null && !preValidation(state, action))
        {
            throw new InvalidOperationException($"Action {action.GetType().Name} failed pre-validation");
        }
        
        await next();
        
        // Validate state after processing
        if (postValidation != null && !postValidation(state))
        {
            throw new InvalidOperationException($"State invalid after action {action.GetType().Name}");
        }
    }
}
