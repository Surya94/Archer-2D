using UnityEngine;
using System;
using System.Collections.Generic;

public class SignalManager : Singleton<SignalManager>
{
    private Dictionary<Type, List<object>> parameterizedObservers = new Dictionary<Type, List<object>>();
    private Dictionary<Type, List<Action>> parameterlessObservers = new Dictionary<Type, List<Action>>();

    public void AddObserver<T>(Action<T> observer) where T : class
    {
        Type eventType = typeof(T);

        if (!parameterizedObservers.ContainsKey(eventType))
        {
            parameterizedObservers[eventType] = new List<object>();
        }

        List<object> observerList = parameterizedObservers[eventType];
        observerList.Add(observer);
    }

    public void AddObserver<T>(Action observer) where T : class
    {
        Type eventType = typeof(T);

        if (!parameterlessObservers.ContainsKey(eventType))
        {
            parameterlessObservers[eventType] = new List<Action>();
        }

        List<Action> observerList = parameterlessObservers[eventType];
        observerList.Add(observer);
    }

    public void RemoveObserver<T>(Action<T> observer) where T : class
    {
        Type eventType = typeof(T);

        if (parameterizedObservers.ContainsKey(eventType))
        {
            List<object> observerList = parameterizedObservers[eventType];
            observerList.Remove(observer);

            if (observerList.Count == 0)
            {
                parameterizedObservers.Remove(eventType);
            }
        }
    }

    public void RemoveObserver<T>(Action observer) where T : class
    {
        Type eventType = typeof(T);

        if (parameterlessObservers.ContainsKey(eventType))
        {
            List<Action> observerList = parameterlessObservers[eventType];
            observerList.Remove(observer);

            if (observerList.Count == 0)
            {
                parameterlessObservers.Remove(eventType);
            }
        }
    }

    public void DispatchSignal<T>(T signalData) where T : class
    {
        Type eventType = typeof(T);

        if (parameterizedObservers.ContainsKey(eventType))
        {
            List<object> observerList = parameterizedObservers[eventType];

            foreach (var observer in observerList)
            {
                ((Action<T>)observer).Invoke(signalData);
            }
        }

        if (parameterlessObservers.ContainsKey(eventType))
        {
            List<Action> observerList = parameterlessObservers[eventType];

            foreach (var observer in observerList)
            {
                observer.Invoke();
            }
        }
    }

    public void DispatchSignal<T>() where T : class
    {
        Type eventType = typeof(T);

        if (parameterlessObservers.ContainsKey(eventType))
        {
            List<Action> observerList = parameterlessObservers[eventType];

            foreach (var observer in observerList)
            {
                observer.Invoke();
            }
        }
    }
}
