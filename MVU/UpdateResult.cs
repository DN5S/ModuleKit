using System.Collections.Generic;

namespace ModuleKit.MVU;

public record UpdateResult<TState>(TState NewState, IReadOnlyList<IEffect> Effects) 
    where TState : IState
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly IReadOnlyList<IEffect> EmptyEffects = new List<IEffect>(0).AsReadOnly();
    
    /// <summary>
    /// Returns a result with an updated state but no side effects
    /// </summary>
    public static UpdateResult<TState> StateOnly(TState state) => 
        new(state, EmptyEffects);
    
    /// <summary>
    /// Returns a result with updated state and side effects
    /// </summary>
    public static UpdateResult<TState> WithEffects(TState state, params IEffect[] effects) => 
        new(state, effects);
    
    /// <summary>
    /// Alias for StateOnly - indicates no changes were made
    /// </summary>
    public static UpdateResult<TState> NoChange(TState state) => 
        StateOnly(state);
}
