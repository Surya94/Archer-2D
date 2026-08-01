using UnityEngine;
using System.Threading;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static readonly object lockObject = new object();
    private static bool applicationIsQuitting = false;

    public static T Instance
    {
        get
        {
            if (applicationIsQuitting)
            {
                Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again.");
                return null;
            }

            lock (lockObject)
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<T>();

                    if (instance == null)
                    {
                        GameObject singletonObject = new GameObject();
                        singletonObject.name = $"{typeof(T).Name} (Singleton)";
                        instance = singletonObject.AddComponent<T>();

                        // Ensure singleton persists across scenes
                        DontDestroyOnLoad(singletonObject);

                        Debug.Log($"[Singleton] Created new instance of {typeof(T).Name}");
                    }
                    else
                    {
                        Debug.Log($"[Singleton] Found existing instance of {typeof(T).Name}");
                    }
                }
                return instance;
            }
        }
    }

    protected virtual void Awake()
    {
        lock (lockObject)
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Debug.LogWarning($"[Singleton] Duplicate instance of {typeof(T).Name} destroyed");
                Destroy(gameObject);
            }
        }
    }

    protected virtual void OnApplicationQuit()
    {
        applicationIsQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}