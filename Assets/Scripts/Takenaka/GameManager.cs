using UnityEngine;
using UnityEngine.AI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("プレイヤー設定")]
    public GameObject playerPrefab;

    [Header("スポーン範囲設定（長方形）")]
    [Tooltip("スポーン範囲の中心座標")]
    public Vector3 spawnCenter = Vector3.zero;
    [Tooltip("中心からX軸方向への広さ（横幅の半分）")]
    public float rangeX = 20.0f;
    [Tooltip("中心からZ軸方向への広さ（奥行きの半分）")]
    public float rangeZ = 20.0f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (GameObject.FindGameObjectWithTag("Player") == null)
        {
            RespawnPlayer();
        }
    }

    public void RequestRespawn(GameObject currentPlayer)
    {
        if (currentPlayer != null)
        {
            Destroy(currentPlayer);
        }
        RespawnPlayer();
    }

    public void RespawnPlayer()
    {
        if (playerPrefab == null) return;

        Vector3 spawnPos = GetRandomNavMeshPosition();

        // 生成
        Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        Debug.Log("<color=green>プレイヤーをリスポーンさせました</color>");
    }

    Vector3 GetRandomNavMeshPosition()
    {
        // 最大100回試行して、正しいNavMeshの上を見つける
        for (int i = 0; i < 100; i++)
        {
            // 四角形の範囲内でランダムな座標を作成
            float randomX = Random.Range(-rangeX, rangeX);
            float randomZ = Random.Range(-rangeZ, rangeZ);

            Vector3 randomPos = spawnCenter + new Vector3(randomX, 0, randomZ);

            // その座標の「すぐ足元（5m以内）」にNavMeshがあるか確認
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, 5.0f, NavMesh.AllAreas))
            {
                return hit.position + Vector3.up * 0.5f;
            }
        }

        Debug.LogWarning("有効なスポーン地点が見つかりませんでした。中心に生成します。");
        return spawnCenter;
    }

    // エディタ上でスポーン範囲を「黄色い四角」で表示する
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 size = new Vector3(rangeX * 2, 1, rangeZ * 2);
        Gizmos.DrawWireCube(spawnCenter, size);
    }
}