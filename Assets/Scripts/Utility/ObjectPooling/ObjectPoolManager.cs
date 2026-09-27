using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    private Dictionary<PoolableTypes, Stack<PoolableObject>> objectPools = new Dictionary<PoolableTypes, Stack<PoolableObject>>();

    protected override void Awake()
    {
        base.Awake();
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        base.OnDestroy();
    }

    /// <summary>
    /// This manager persists across scenes but every pooled instance is a plain Instantiate
    /// into the active scene, so a scene unload destroys all of them while the stacks keep
    /// holding the dead wrappers. Without this, the next SpawnObject pops a corpse and throws
    /// on obj.transform. Clearing is simply correct: Spawn then falls back to Instantiate
    /// exactly as it does on a fresh load.
    /// </summary>
    private void OnSceneUnloaded(Scene scene)
    {
        objectPools.Clear();
    }

    /// <summary>
    /// Unity-null-safe name for logging. A plain `obj?.name` is NOT safe here: `?.` performs a
    /// real null check, which bypasses Unity's overloaded == fake-null, so on a DESTROYED object
    /// it proceeds to call .name and throws a NullReferenceException - turning an error report
    /// into a crash.
    /// </summary>
    private static string SafeName(UnityEngine.Object obj)
    {
        return obj == null ? "<destroyed or null>" : obj.name;
    }

    /// <summary>
    /// True when the reference is non-null AND the underlying native object is still alive.
    /// Takes UnityEngine.Object rather than a generic T on purpose: comparing a generic type
    /// parameter against null can compile to plain reference equality, which does NOT see
    /// Unity's fake-null and so treats a destroyed object as alive.
    /// </summary>
    private static bool IsAlive(UnityEngine.Object obj)
    {
        return obj != null;
    }

    public void PrepopulatePool<T>(T prefab, int count) where T : PoolableObject
    {
        if (!IsAlive(prefab) || prefab.Poolable == null)
        {
            Debug.LogError($"Invalid prefab or poolable type for {SafeName(prefab)}");
            return;
        }

        if (!objectPools.TryGetValue(prefab.Poolable, out Stack<PoolableObject> pool))
        {
            pool = new Stack<PoolableObject>();
            objectPools[prefab.Poolable] = pool;
        }

        for (int i = 0; i < count; i++)
        {
            T newObj = Instantiate(prefab);
            newObj.gameObject.SetActive(false);
            pool.Push(newObj);
        }
    }

    public T SpawnObject<T>(T prefab, Vector3 position, Quaternion rotation) where T : PoolableObject
    {
        if (!IsAlive(prefab) || prefab.Poolable == null)
        {
            Debug.LogError($"Invalid prefab or poolable type for {SafeName(prefab)}");
            return null;
        }

        if (!objectPools.TryGetValue(prefab.Poolable, out Stack<PoolableObject> pool))
        {
            pool = new Stack<PoolableObject>();
            objectPools[prefab.Poolable] = pool;
        }

        T obj = null;

        // Discard any destroyed entries left over from a previous scene before trusting the
        // pool. The sceneUnloaded handler above should have cleared them, but a pool can also
        // be poisoned by an object destroyed individually while pooled. The liveness test runs
        // on PoolableObject, not on T, so Unity's fake-null is actually honoured.
        while (obj == null && pool.Count > 0)
        {
            PoolableObject candidate = pool.Pop();

            if (IsAlive(candidate))
                obj = candidate as T;
        }

        if (obj == null)
        {
            obj = Instantiate(prefab);
        }

        // Set position and rotation
        obj.transform.position = position;
        obj.transform.rotation = rotation;

        // Enable and spawn
        obj.gameObject.SetActive(true);
        obj.OnObjectSpawn();

        return obj;
    }

    public T SpawnObject<T>(T prefab, Vector3 position) where T : PoolableObject
    {
        return SpawnObject(prefab, position, Quaternion.identity);
    }

    public T SpawnObject<T>(T prefab) where T : PoolableObject
    {
        return SpawnObject(prefab, Vector3.zero, Quaternion.identity);
    }

    public void DespawnObject(PoolableObject obj)
    {
        if (!IsAlive(obj) || obj.Poolable == null)
        {
            Debug.LogError("Cannot despawn null object or object without poolable type");
            return;
        }

        // Call despawn callback
        obj.OnObjectDespawn();

        // Simply disable the object
        obj.gameObject.SetActive(false);

        // Return to pool
        if (objectPools.TryGetValue(obj.Poolable, out Stack<PoolableObject> pool))
        {
            pool.Push(obj);
        }
        else
        {
            Debug.LogError($"Pool not found for type {SafeName(obj.Poolable)}");
        }
    }
}