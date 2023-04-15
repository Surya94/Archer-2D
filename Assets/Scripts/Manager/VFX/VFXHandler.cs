using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VFXHandler : MonoBehaviour
{
    public GameObject vfxObj;
    public Canvas canvas;
    public Queue<GameObject> vfxobjectPool;

    public void Start()
    {
        Init();
    }

    public void OnDestory()
    {
        Dinit();
    }

    public virtual void Init()
    {
        vfxobjectPool = new Queue<GameObject>();
    }
 
    public virtual void Dinit()
    {
      
    }

    public GameObject GetUnusedObject()
    {

        GameObject spawnedObject;
        if (vfxobjectPool.Count > 0)
        {
            spawnedObject = vfxobjectPool.Dequeue();
            spawnedObject.SetActive(true);
        }
        else
        {
            spawnedObject = Instantiate(vfxObj, canvas.transform).gameObject;
        }
        return spawnedObject;
    }

    public void ReturnObjectVFXToPool(GameObject obj)
    {
        obj.SetActive(false);
        vfxobjectPool.Enqueue(obj);
    }
}
