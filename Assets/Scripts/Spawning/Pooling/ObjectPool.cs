using System.Collections.Generic;
using UnityEngine;

public class ObjectPool
{
    private readonly PooledObject prefab;
    private readonly Transform parent;

    private readonly Queue<PooledObject> availableObjects = new();


    public ObjectPool(
        PooledObject prefab,
        int initialSize,
        Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;

        CreateObjects(initialSize);
    }


    private void CreateObjects(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            PooledObject instance =
                Object.Instantiate(prefab, parent);

            instance.SetActive(false);

            availableObjects.Enqueue(instance);
        }
    }


    public PooledObject Get()
    {
        PooledObject instance;

        if (availableObjects.Count > 0)
        {
            instance = availableObjects.Dequeue();
        }
        else
        {
            instance = Object.Instantiate(prefab, parent);
        }

        return instance;
    }


    public void Release(PooledObject instance)
    {
        instance.SetActive(false);

        instance.transform.SetParent(parent, false);

        availableObjects.Enqueue(instance);
    }
}