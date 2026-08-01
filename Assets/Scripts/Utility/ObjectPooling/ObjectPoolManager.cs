using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    private Dictionary<PoolableTypes, Stack<PoolableObject>> objectPools = new Dictionary<PoolableTypes, Stack<PoolableObject>>();

    public void PrepopulatePool<T>(T prefab, int count) where T : PoolableObject
    {
        if (prefab == null || prefab.Poolable == null)
        {
            Debug.LogError($"Invalid prefab or poolable type for {prefab?.name}");
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
        if (prefab == null || prefab.Poolable == null)
        {
            Debug.LogError($"Invalid prefab or poolable type for {prefab?.name}");
            return null;
        }

        if (!objectPools.TryGetValue(prefab.Poolable, out Stack<PoolableObject> pool))
        {
            pool = new Stack<PoolableObject>();
            objectPools[prefab.Poolable] = pool;
        }

        T obj;

        if (pool.Count > 0)
        {
            obj = (T)pool.Pop();
        }
        else
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
        if (obj == null || obj.Poolable == null)
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
            Debug.LogError($"Pool not found for type {obj.Poolable.name}");
        }
    }
}