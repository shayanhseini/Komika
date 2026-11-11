using System;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    [Serializable]
    private class PoolConfig
    {
        public PoolType type;
        public PooledObject prefab;
        public int amountPerChunk;
    }
    
    [Header("References")]
    [SerializeField] private ChunkManager chunkManager;


    [Header("Pools")]
    [SerializeField] private PoolConfig[] poolConfigs;


    private ObjectPool[] pools;


    public int InitialChunkCount =>
        chunkManager.InitialChunkCount;


    private void Awake()
    {
        InitializePools();
    }


    private void InitializePools()
    {
        int chunkCount =
            chunkManager.InitialChunkCount;


        pools = new ObjectPool[poolConfigs.Length];


        for (int i = 0; i < poolConfigs.Length; i++)
        {
            PoolConfig config = poolConfigs[i];


            int poolSize =
                chunkCount * config.amountPerChunk;


            pools[i] = new ObjectPool(
                config.prefab,
                poolSize,
                transform
            );
        }
    }


    public PooledObject Get(PoolType type)
    {
        int index = GetPoolIndex(type);

        if (index == -1)
        {
            Debug.LogError(
                $"Pool with type {type} was not found."
            );

            return null;
        }


        return pools[index].Get();
    }


    public void Release(PooledObject pooledObject)
    {
        if (pooledObject == null)
            return;


        int index =
            GetPoolIndex(pooledObject.PoolType);


        if (index == -1)
        {
            Debug.LogError(
                $"Pool with type {pooledObject.PoolType} was not found."
            );

            return;
        }


        pools[index].Release(pooledObject);
    }


    private int GetPoolIndex(PoolType type)
    {
        for (int i = 0; i < poolConfigs.Length; i++)
        {
            if (poolConfigs[i].type == type)
                return i;
        }


        return -1;
    }
}