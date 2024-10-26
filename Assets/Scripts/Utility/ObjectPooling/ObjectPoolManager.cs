using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    private Dictionary<PoolableTypes, Stack<PoolableObject>> objectPools = new Dictionary<PoolableTypes, Stack<PoolableObject>>();

    public T SpawnObject<T>(T prefab, Vector3 position, Quaternion rotation) where T : PoolableObject
    {
        Type objectType = typeof(T);
        if (prefab.Poolable == null)
        {
            Debug.LogError($"Error => Poolable type is not assigned for {prefab.name}");
            return null;
        }
        if (objectPools.TryGetValue(prefab.Poolable, out Stack<PoolableObject> pool))
        {
            if (pool.Count > 0)
            {
                T obj = (T)pool.Pop();
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.OnObjectSpawn();
                obj.gameObject.SetActive(true);
                return obj;
            }
        }
        else
        {
            objectPools[prefab.Poolable] = new Stack<PoolableObject>();
        }

        T newObj = Instantiate(prefab, position, rotation);
        newObj.OnObjectSpawn();
        return newObj;
    }

    public void DespawnObject(PoolableObject obj)
    {
        obj.OnObjectDespawn();
        obj.gameObject.SetActive(false);

        Type objectType = obj.GetType();
        if (objectPools.TryGetValue(obj.Poolable, out Stack<PoolableObject> pool))
        {
            pool.Push(obj);
        }
        else
        {
            // Handle error: Object type not found in the pool
        }
    }
}
