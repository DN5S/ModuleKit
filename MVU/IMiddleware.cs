using System;
using System.Threading.Tasks;

namespace ModuleKit.MVU;

public interface IMiddleware<in TState> where TState : IState
{
    Task ProcessAsync(TState state, IAction action, Func<Task> next);
}
