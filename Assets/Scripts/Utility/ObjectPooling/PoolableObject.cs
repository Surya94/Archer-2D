using UnityEngine;

public class PoolableObject : MonoBehaviour
{
    [Header("Poolable Information")]
    public PoolableTypes Poolable;

    public virtual void OnObjectSpawn()
    {
        // Override in derived classes if needed
    }

    public virtual void OnObjectDespawn()
    {
        // Override in derived classes if needed
    }
}