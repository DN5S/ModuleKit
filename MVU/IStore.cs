using System;
using System.Threading.Tasks;

namespace ModuleKit.MVU;

public interface IStore
{
    void Dispatch(IAction action);
    Task DispatchAsync(IAction action);
}

public interface IStore<out TState> : IStore where TState : IState
{
    TState State { get; }
    IObservable<TState> StateChanged { get; }
    IObservable<IAction> ActionDispatched { get; }
}
