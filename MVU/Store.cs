using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace ModuleKit.MVU;

public delegate UpdateResult<TState> UpdateFunction<TState>(TState state, IAction action) 
    where TState : IState;

public delegate Task MiddlewareDelegate<in TState>(TState state, IAction action, Func<Task> next) 
    where TState : IState;

public class Store<TState>(TState initialState, UpdateFunction<TState> updateFunction) : IStore<TState>, IDisposable
    where TState : IState
{
    private readonly List<MiddlewareDelegate<TState>> middlewares = [];
    private readonly Dictionary<Type, object> effectHandlers = new();
    private readonly BehaviorSubject<TState> stateSubject = new(initialState);
    private readonly Subject<IAction> actionSubject = new();
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private long version;
    
    public TState State { get; private set; } = initialState;

    public IObservable<TState> StateChanged => stateSubject;
    public IObservable<IAction> ActionDispatched => actionSubject;

    public void UseMiddleware(MiddlewareDelegate<TState> middleware)
    {
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
               .Reverse<MiddlewareDelegate<TState>>()
               .Aggregate(
                   (Func<Task>)CoreUpdate,
                   (next, middleware) => () => middleware(State, action, next)
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
        
        if (versionProp?.CanWrite == true)
        {
            if (type.IsRecord())
            {
                // Create a shallow copy and update the version
                var newState = (TState)state.Clone();
                versionProp.SetValue(newState, newVersion);
                return newState;
            }
            versionProp.SetValue(state, newVersion);
        }
        return state;
    }
    
    public void Dispose()
    {
        stateSubject.Dispose();
        actionSubject.Dispose();
        semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
