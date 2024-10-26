using UnityEngine;

public class PoolableObject : MonoBehaviour
{
  
    [Header("Poolable Information")]
    public PoolableTypes Poolable;

    public virtual void OnObjectSpawn() { }
    public virtual void OnObjectDespawn() { }
}