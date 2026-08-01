using UnityEngine;

[CreateAssetMenu(fileName = "PoolableType", menuName = "ScriptableObjects/Poolable Types", order = 1)]
public class PoolableTypes : ScriptableObject
{
    [SerializeField] private string description;

    public string Description => description;
}