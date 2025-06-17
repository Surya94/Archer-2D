using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    private Dictionary<PoolableTypes, Stack<PoolableObject>> objectPools = new Dictionary<PoolableTypes, Stack<PoolableObject>>();

    public void PrepopulatePool<T>(T prefab, int count) where T : PoolableObject
    {
        if (prefab.Poolable == null)
        {
            Debug.LogError($"Error => Poolable type is not assigned for {prefab.name}");
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
        if (prefab.Poolable == null)
        {
            Debug.LogError($"Error => Poolable type is not assigned for {prefab.name}");
            return null;
        }

        if (!objectPools.TryGetValue(prefab.Poolable, out Stack<PoolableObject> pool))
        {
            pool = new Stack<PoolableObject>();
            objectPools[prefab.Poolable] = pool;
        }

        if (pool.Count > 0)
        {
            T obj = (T)pool.Pop();
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.OnObjectSpawn();
            obj.gameObject.SetActive(true);
            return obj;
        }

        T newObj = Instantiate(prefab, position, rotation);
        newObj.OnObjectSpawn();
        return newObj;
    }

    public void DespawnObject(PoolableObject obj)
    {
        obj.OnObjectDespawn();
        obj.gameObject.SetActive(false);

        if (objectPools.TryGetValue(obj.Poolable, out Stack<PoolableObject> pool))
        {
            pool.Push(obj);
        }
        else
        {
            Debug.LogError($"Error => Poolable type {obj.Poolable.name} not found in the pool");
        }
    }
}
