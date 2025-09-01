using System;

namespace TLink.Core.Reactive;

/// <summary>
/// Contract for event bus implementations
/// Enables decorator pattern for monitoring and other cross-cutting concerns
/// </summary>
public interface IEventBus : IDisposable
{
    /// <summary>
    /// Publish an event to all subscribers
    /// </summary>
    void Publish<TEvent>(TEvent eventData) where TEvent : class;
    
    /// <summary>
    /// Subscribe to events of a specific type
    /// </summary>
    IObservable<TEvent> Listen<TEvent>() where TEvent : class;
}