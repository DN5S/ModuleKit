using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace ModuleKit.MVU;

public delegate UpdateResult<TState> UpdateFunction<TState>(TState state, IAction action) 
    where TState : IState;

public class Store<TState>(TState initialState, UpdateFunction<TState> updateFunction) : IStore<TState>, IDisposable
    where TState : IState
{
    private readonly List<IMiddleware<TState>> middlewares = [];
    private readonly Dictionary<Type, object> effectHandlers = new();
    private readonly BehaviorSubject<TState> stateSubject = new(initialState);
    private readonly Subject<IAction> actionSubject = new();
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private long version;
    
    public TState State { get; private set; } = initialState;

    public IObservable<TState> StateChanged => stateSubject;
    public IObservable<IAction> ActionDispatched => actionSubject;

    public void UseMiddleware(IMiddleware<TState> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        middlewares.Add(middleware);
    }
    
    public void RegisterEffectHandler<TEffect>(IEffectHandler<TEffect> handler) 
        where TEffect : IEffect
    {
        effectHandlers[typeof(TEffect)] = handler;
    }
    
    public void Dispatch(IAction action)
    {
        Task.Run(async () => await DispatchAsync(action).ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
    
    public async Task DispatchAsync(IAction action)
    {
        await semaphore.WaitAsync();
        try
        {
            actionSubject.OnNext(action);
            
            var middlewarePipeline = BuildMiddlewarePipeline(action);
            await middlewarePipeline();
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    private Func<Task> BuildMiddlewarePipeline(IAction action)
    {
        return middlewares
               .AsEnumerable()
               .Reverse()
               .Aggregate(
                   (Func<Task>)CoreUpdate,
                   (next, middleware) => () => middleware.ProcessAsync(State, action, next)
               );

        async Task CoreUpdate()
        {
            var result = updateFunction(State, action);
            
            if (!ReferenceEquals(result.NewState, State))
            {
                version++;
                State = Store<TState>.SetVersion(result.NewState, version);
                stateSubject.OnNext(State);
            }
            
            foreach (var effect in result.Effects)
            {
                await HandleEffect(effect);
            }
        }
    }
    
    private async Task HandleEffect(IEffect effect)
    {
        var effectType = effect.GetType();
        var handlerType = typeof(IEffectHandler<>).MakeGenericType(effectType);
        
        if (effectHandlers.TryGetValue(effectType, out var handler))
        {
            var handleMethod = handlerType.GetMethod("HandleAsync");
            if (handleMethod != null)
            {
                if (handleMethod.Invoke(handler, [effect, this]) is Task task)
                    await task;
            }
        }
    }
    
    private static TState SetVersion(TState state, long newVersion)
    {
        var type = state.GetType();
        var versionProp = type.GetProperty(nameof(IState.Version));
        
        if (versionProp?.CanWrite != true)
            return state;

        if (type.IsRecord())
        {
            // Records have built-in cloning support
            var clonedState = (TState)state.Clone();
            versionProp.SetValue(clonedState, newVersion);
            return clonedState;
        }
        try
        {
            // Try to create a new instance via copy constructor if available
            var copyConstructor = type.GetConstructor([type]);
            TState copiedState;
            if (copyConstructor != null)
            {
                copiedState = (TState)copyConstructor.Invoke([state]);
            }
            else
            {
                // Use JSON serialization as a fallback for deep copy
                var json = System.Text.Json.JsonSerializer.Serialize(state);
                copiedState = System.Text.Json.JsonSerializer.Deserialize<TState>(json)!;
            }
            
            versionProp.SetValue(copiedState, newVersion);
            return copiedState;
        }
        catch
        {
            // If all copy attempts fail, throw to prevent state corruption
            throw new InvalidOperationException(
                $"State type {type.Name} must be a record type or provide a copy constructor to ensure immutability. " +
                "Consider using a record type for state objects.");
        }
    }
    
    public void Dispose()
    {
        stateSubject.Dispose();
        actionSubject.Dispose();
        semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
