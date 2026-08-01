using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class SignalManager : Singleton<SignalManager>
{
    private readonly ConcurrentDictionary<Type, List<object>> parameterizedObservers = new ConcurrentDictionary<Type, List<object>>();
    private readonly ConcurrentDictionary<Type, List<Action>> parameterlessObservers = new ConcurrentDictionary<Type, List<Action>>();
    private readonly object lockObject = new object();

    public void AddObserver<T>(Action<T> observer) where T : class
    {
        if (observer == null)
        {
            Debug.LogError("[SignalManager] Cannot add null observer");
            return;
        }

        Type eventType = typeof(T);

        lock (lockObject)
        {
            var observerList = parameterizedObservers.GetOrAdd(eventType, _ => new List<object>());
            observerList.Add(observer);
        }

        Debug.Log($"[SignalManager] Added parameterized observer for {eventType.Name}");
    }

    public void AddObserver<T>(Action observer) where T : class
    {
        if (observer == null)
        {
            Debug.LogError("[SignalManager] Cannot add null observer");
            return;
        }

        Type eventType = typeof(T);

        lock (lockObject)
        {
            var observerList = parameterlessObservers.GetOrAdd(eventType, _ => new List<Action>());
            observerList.Add(observer);
        }

        Debug.Log($"[SignalManager] Added parameterless observer for {eventType.Name}");
    }

    public void RemoveObserver<T>(Action<T> observer) where T : class
    {
        if (observer == null)
        {
            Debug.LogError("[SignalManager] Cannot remove null observer");
            return;
        }

        Type eventType = typeof(T);

        lock (lockObject)
        {
            if (parameterizedObservers.TryGetValue(eventType, out List<object> observerList))
            {
                bool removed = observerList.Remove(observer);

                if (observerList.Count == 0)
                {
                    parameterizedObservers.TryRemove(eventType, out _);
                }

                if (removed)
                {
                    Debug.Log($"[SignalManager] Removed parameterized observer for {eventType.Name}");
                }
            }
        }
    }

    public void RemoveObserver<T>(Action observer) where T : class
    {
        if (observer == null)
        {
            Debug.LogError("[SignalManager] Cannot remove null observer");
            return;
        }

        Type eventType = typeof(T);

        lock (lockObject)
        {
            if (parameterlessObservers.TryGetValue(eventType, out List<Action> observerList))
            {
                bool removed = observerList.Remove(observer);

                if (observerList.Count == 0)
                {
                    parameterlessObservers.TryRemove(eventType, out _);
                }

                if (removed)
                {
                    Debug.Log($"[SignalManager] Removed parameterless observer for {eventType.Name}");
                }
            }
        }
    }

    public void DispatchSignal<T>(T signalData) where T : class
    {
        if (signalData == null)
        {
            Debug.LogError("[SignalManager] Cannot dispatch null signal data");
            return;
        }

        Type eventType = typeof(T);
        List<object> parameterizedCopy = null;
        List<Action> parameterlessCopy = null;

        // Create copies of observer lists to avoid modification during iteration
        lock (lockObject)
        {
            if (parameterizedObservers.TryGetValue(eventType, out List<object> parameterizedList))
            {
                parameterizedCopy = new List<object>(parameterizedList);
            }

            if (parameterlessObservers.TryGetValue(eventType, out List<Action> parameterlessList))
            {
                parameterlessCopy = new List<Action>(parameterlessList);
            }
        }

        // Invoke observers outside of lock to prevent deadlocks
        InvokeParameterizedObservers(parameterizedCopy, signalData, eventType);
        InvokeParameterlessObservers(parameterlessCopy, eventType);
    }

    public void DispatchSignal<T>() where T : class
    {
        Type eventType = typeof(T);
        List<Action> parameterlessCopy = null;

        lock (lockObject)
        {
            if (parameterlessObservers.TryGetValue(eventType, out List<Action> parameterlessList))
            {
                parameterlessCopy = new List<Action>(parameterlessList);
            }
        }

        InvokeParameterlessObservers(parameterlessCopy, eventType);
    }

    private void InvokeParameterizedObservers<T>(List<object> observers, T signalData, Type eventType) where T : class
    {
        if (observers == null) return;

        foreach (var observer in observers)
        {
            try
            {
                ((Action<T>)observer)?.Invoke(signalData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalManager] Error invoking parameterized observer for {eventType.Name}: {ex.Message}");
            }
        }
    }

    private void InvokeParameterlessObservers(List<Action> observers, Type eventType)
    {
        if (observers == null) return;

        foreach (var observer in observers)
        {
            try
            {
                observer?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalManager] Error invoking parameterless observer for {eventType.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Clear all observers for a specific event type
    /// </summary>
    public void ClearObservers<T>() where T : class
    {
        Type eventType = typeof(T);

        lock (lockObject)
        {
            parameterizedObservers.TryRemove(eventType, out _);
            parameterlessObservers.TryRemove(eventType, out _);
        }

        Debug.Log($"[SignalManager] Cleared all observers for {eventType.Name}");
    }

    /// <summary>
    /// Clear all observers
    /// </summary>
    public void ClearAllObservers()
    {
        lock (lockObject)
        {
            parameterizedObservers.Clear();
            parameterlessObservers.Clear();
        }

        Debug.Log("[SignalManager] Cleared all observers");
    }

    /// <summary>
    /// Get debug information about current observers
    /// </summary>
    public string GetDebugInfo()
    {
        lock (lockObject)
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Signal Manager Debug Info ===");
            info.AppendLine($"Parameterized Observer Types: {parameterizedObservers.Count}");
            info.AppendLine($"Parameterless Observer Types: {parameterlessObservers.Count}");

            foreach (var kvp in parameterizedObservers)
            {
                info.AppendLine($"  {kvp.Key.Name}: {kvp.Value.Count} parameterized observers");
            }

            foreach (var kvp in parameterlessObservers)
            {
                info.AppendLine($"  {kvp.Key.Name}: {kvp.Value.Count} parameterless observers");
            }

            return info.ToString();
        }
    }

    protected override void OnDestroy()
    {
        ClearAllObservers();
        base.OnDestroy();
    }
}