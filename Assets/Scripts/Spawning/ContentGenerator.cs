using UnityEngine;

public class ContentGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PoolManager poolManager;


    [Header("Generation")]
    [SerializeField] private int minCoins = 3;
    [SerializeField] private int maxCoins = 8;

    [SerializeField] private int minObstacles = 0;
    [SerializeField] private int maxObstacles = 2;


    [Header("Spawn Area")]
    [SerializeField] private float minX = -8f;
    [SerializeField] private float maxX = 8f;

    [SerializeField] private float minY = -4f;
    [SerializeField] private float maxY = 4f;


    public void Generate(WorldChunk chunk)
    {
        GenerateCoins(chunk);
        GenerateObstacles(chunk);
    }


    public void Clear(WorldChunk chunk)
    {
        for (int i = chunk.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = chunk.transform.GetChild(i);

            PooledObject pooledObject =
                child.GetComponent<PooledObject>();

            if (pooledObject == null)
                continue;

            poolManager.Release(pooledObject);
        }
    }


    private void GenerateCoins(WorldChunk chunk)
    {
        int amount = Random.Range(
            minCoins,
            maxCoins + 1
        );


        for (int i = 0; i < amount; i++)
        {
            PooledObject coin =
                poolManager.Get(PoolType.Coin);

            if (coin == null)
                continue;


            SetupObject(coin, chunk);
        }
    }


    private void GenerateObstacles(WorldChunk chunk)
    {
        int amount = Random.Range(
            minObstacles,
            maxObstacles + 1
        );


        for (int i = 0; i < amount; i++)
        {
            PooledObject obstacle =
                poolManager.Get(PoolType.Obstacle);

            if (obstacle == null)
                continue;


            SetupObject(obstacle, chunk);
        }
    }


    private void SetupObject(
        PooledObject pooledObject,
        WorldChunk chunk)
    {
        pooledObject.transform.SetParent(
            chunk.transform,
            false
        );


        pooledObject.transform.localPosition =
            GetRandomLocalPosition(chunk);


        pooledObject.SetActive(true);
    }


    private Vector3 GetRandomLocalPosition(WorldChunk chunk)
    {
        float x = Random.Range(minX, maxX);

        float y = Random.Range(minY, maxY);

        float z = Random.Range(
            0f,
            chunk.Length
        );


        return new Vector3(x, y, z);
    }
}