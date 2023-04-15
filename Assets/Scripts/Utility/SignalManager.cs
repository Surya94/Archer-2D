using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SignalManager : Singleton<SignalManager>
{

    public delegate void SignalEvent<T>(T signalData) where T : class;
    private Dictionary<System.Type, object> signals;

    public SignalManager()
    {
        signals = new Dictionary<System.Type, object>();
    }

    public void AddListener<T>(SignalEvent<T> listener) where T : class
    {
        SignalEvent<T> signalEvent;
        System.Type signalType = typeof(T);
        if (signals.TryGetValue(signalType, out object signal))
        {
            signalEvent = (SignalEvent<T>)signal;
            signalEvent += listener;
        }
        else
        {
            signalEvent = listener;
        }
        signals[signalType] = signalEvent;
    }

    public void RemoveListener<T>(SignalEvent<T> listener) where T : class
    {
        System.Type signalType = typeof(T);
        if (signals.ContainsKey(signalType))
        {
            SignalEvent<T> signalEvent = (SignalEvent<T>)signals[signalType];
            signalEvent -= listener;
            signals[signalType] = signalEvent;
        }
    }

    public void DispatchSignal<T>(T signalData) where T : class
    {
        SignalEvent<T> signalEvent;
        System.Type signalType = typeof(T);
        if (signals.TryGetValue(signalType, out object signal))
        {
            signalEvent = (SignalEvent<T>)signal;
            signalEvent.Invoke(signalData);
        }
    }
}
