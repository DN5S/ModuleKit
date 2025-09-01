using System.Threading.Tasks;

namespace TLink.Core.MVU;

/// <summary>
/// Represents a side effect that should be executed after a state update.
/// Effects are used for operations like API calls, file I/O, notifications, etc.
/// Example use cases:
/// - SaveConfigurationEffect: Persists state to disk
/// - NotifyConfigurationChangedEffect: Publishes events to other modules
/// - FetchDataEffect: Makes HTTP requests to fetch data
/// - LogEffect: Writes to log files
/// </summary>
public interface IEffect
{
    string Type { get; }
}

/// <summary>
/// Handles the execution of a specific effect type.
/// Registered with the Store to process effects after state updates.
/// </summary>
public interface IEffectHandler<in TEffect> where TEffect : IEffect
{
    Task HandleAsync(TEffect effect, IStore store);
}
