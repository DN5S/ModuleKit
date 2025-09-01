using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace TLink.Core.Reactive;

public class EventBus : IEventBus
{
    private readonly Subject<object> subject = new();
    private readonly Dictionary<Type, object> replaySubjects = new();
    private readonly Lock lockObject = new();
    private bool isDisposed;
    
    public void Publish<T>(T message) where T : class
    {
        if (!isDisposed)
        {
            subject.OnNext(message);

            // Also publish to a replay subject if one exists for this type
            lock (lockObject)
            {
                if (replaySubjects.TryGetValue(typeof(T), out var replaySubject))
                {
                    ((ReplaySubject<T>)replaySubject).OnNext(message);
                }
            }
        }
        else
            throw new ObjectDisposedException(nameof(EventBus));
    }
    
    public IObservable<T> Listen<T>() where T : class
    {
        return isDisposed ? throw new ObjectDisposedException(nameof(EventBus)) : subject.OfType<T>();
    }
    
    public IObservable<T> ListenLatest<T>(T initialValue) where T : notnull
    {
        return isDisposed ? throw new ObjectDisposedException(nameof(EventBus)) : subject.OfType<T>().StartWith(initialValue);
    }
    
    public IObservable<T> ListenWithReplay<T>(int bufferSize = 1) where T : notnull
    {
        if (!isDisposed)
        {
            lock (lockObject)
            {
                if (!replaySubjects.ContainsKey(typeof(T)))
                {
                    replaySubjects[typeof(T)] = new ReplaySubject<T>(bufferSize);
                }

                var replaySubject = (ReplaySubject<T>)replaySubjects[typeof(T)];
                return replaySubject.AsObservable();
            }
        }

        throw new ObjectDisposedException(nameof(EventBus));
    }
    
    public void ClearReplayBuffer<T>() where T : notnull
    {
        lock (lockObject)
        {
            if (replaySubjects.TryGetValue(typeof(T), out var replaySubject))
            {
                ((IDisposable)replaySubject).Dispose();
                replaySubjects.Remove(typeof(T));
            }
        }
    }
    
    public void Dispose()
    {
        if (isDisposed)
            return;
            
        isDisposed = true;
        
        subject.OnCompleted();
        subject.Dispose();
        
        lock (lockObject)
        {
            foreach (var kvp in replaySubjects)
            {
                ((IDisposable)kvp.Value).Dispose();
            }
            replaySubjects.Clear();
        }
        
        GC.SuppressFinalize(this);
    }
}
