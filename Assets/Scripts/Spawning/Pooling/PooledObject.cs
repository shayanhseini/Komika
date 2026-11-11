using UnityEngine;

public class PooledObject : MonoBehaviour
{
    [SerializeField] private PoolType poolType;

    public PoolType PoolType => poolType;


    public void SetActive(bool value)
    {
        gameObject.SetActive(value);
    }
}