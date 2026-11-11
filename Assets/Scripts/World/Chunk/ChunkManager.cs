using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldChunk[] chunks;
    [SerializeField] private Transform player;
    [SerializeField] private WorldMovementManager worldMovement;
    [SerializeField] private DifficultyManager difficultyManager;

    [Header("Recycle")]
    [SerializeField] private float recycleOffset = 50f;

    public int InitialChunkCount => chunks?.Length ?? 0;

    private void Awake()
    {
        if (chunks == null || chunks.Length == 0)
            return;

        SortChunks();
        AlignChunks();

        for (int i = 0; i < chunks.Length; i++)
        {
            // The first regular chunk replaces the removed start chunk.
            // It starts empty and becomes a normal chunk after recycling.
            chunks[i].ResetContent(difficultyManager, generateContent: i != 0);
        }
    }

    private void Update()
    {
        if (chunks == null || chunks.Length == 0 ||
            worldMovement == null || !worldMovement.IsMovementEnabled)
            return;

        MoveChunks();
        CheckRecycle();
    }

    private void MoveChunks()
    {
        Vector3 movement = Vector3.back * worldMovement.WorldSpeed * Time.deltaTime;

        foreach (WorldChunk chunk in chunks)
            if (chunk != null)
                chunk.Move(movement);
    }

    private void CheckRecycle()
    {
        WorldChunk first = chunks[0];
        if (first == null || player == null)
            return;

        float firstEndZ = first.transform.position.z + first.Length * 0.5f;
        if (firstEndZ < player.position.z - recycleOffset)
            RecycleChunk(first);
    }

    private void RecycleChunk(WorldChunk chunk)
    {
        WorldChunk last = chunks[^1];
        Vector3 position = chunk.transform.position;
        position.z = last.transform.position.z + last.Length;

        chunk.MoveTo(position);
        chunk.ResetContent(difficultyManager);
        ShiftChunks(chunk);
    }

    private void ShiftChunks(WorldChunk recycledChunk)
    {
        for (int i = 0; i < chunks.Length - 1; i++)
            chunks[i] = chunks[i + 1];

        chunks[^1] = recycledChunk;
    }

    private void SortChunks()
    {
        System.Array.Sort(chunks, (a, b) =>
            a.transform.position.z.CompareTo(b.transform.position.z));
    }

    private void AlignChunks()
    {
        for (int i = 1; i < chunks.Length; i++)
        {
            WorldChunk previous = chunks[i - 1];
            WorldChunk current = chunks[i];
            float previousEndZ = previous.transform.position.z + previous.Length * 0.5f;
            Vector3 position = current.transform.position;
            position.z = previousEndZ + current.Length * 0.5f;
            current.MoveTo(position);
        }
    }
}
