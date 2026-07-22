using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [Header("生成設定")]
    public GameObject enemyPrefab;
    public bool spawnOnStart = true;
    public float gridSize = 2.0f;

    private bool hasSpawned = false; // 二重生成防止フラグ

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnEnemy();
        }
    }

    public void SpawnEnemy()
    {
        // 1. すでに生成済みなら何もしない（究極の安全策）
        if (hasSpawned) return;

        if (enemyPrefab == null) return;

        // 2. 詳細なログを出力
        // 自身のオブジェクト名、フルパス、インスタンスIDを表示
        string fullPath = GetGameObjectPath(gameObject);
        Debug.Log($"<color=cyan>【スポーン報告】</color> 名前: {gameObject.name}, パス: {fullPath}, ID: {gameObject.GetInstanceID()}", gameObject);

        Vector3 spawnPos = new Vector3(
            Mathf.Round(transform.position.x / gridSize) * gridSize,
            transform.position.y,
            Mathf.Round(transform.position.z / gridSize) * gridSize
        );

        GameObject spawnedEnemy = Instantiate(enemyPrefab, spawnPos, transform.rotation);
        spawnedEnemy.name = "Enemy_Spawned_" + System.DateTime.Now.ToString("HHmmss");

        hasSpawned = true;
    }

    // オブジェクトの階層（パス）をたどる関数
    private string GetGameObjectPath(GameObject obj)
    {
        string path = "/" + obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = "/" + obj.name + path;
        }
        return path;
    }

    void OnDrawGizmos()
    {
        Vector3 snappedPos = new Vector3(Mathf.Round(transform.position.x / gridSize) * gridSize, transform.position.y, Mathf.Round(transform.position.z / gridSize) * gridSize);
        Gizmos.color = new Color(0.7f, 0f, 1f, 0.8f);
        Gizmos.DrawWireCube(snappedPos + Vector3.up * 1f, new Vector3(0.8f, 2f, 0.8f));
    }
}