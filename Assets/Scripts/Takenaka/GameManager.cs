using UnityEngine;
using UnityEngine.AI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("プレイヤー設定")]
    public GameObject playerPrefab;

    [Header("リスポーン範囲（長方形）")]
    public Vector3 spawnCenter = Vector3.zero;
    public float rangeX = 20f;
    public float rangeZ = 20f;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // シーン内にPlayerタグのオブジェクトがないかチェック
        if (GameObject.FindGameObjectWithTag("Player") == null)
        {
            RespawnPlayer();
        }
    }

    void RespawnPlayer()
    {
        if (playerPrefab == null) return;

        // NavMesh上のランダムな位置を取得
        Vector3 randomPos = GetRandomNavMeshPosition();

        // 生成
        Instantiate(playerPrefab, randomPos, Quaternion.identity);
        Debug.Log("<color=green>プレイヤーがリスポーンしました</color>");
    }

    Vector3 GetRandomNavMeshPosition()
    {
        for (int i = 0; i < 30; i++)
        {
            float rx = Random.Range(-rangeX / 2, rangeX / 2);
            float rz = Random.Range(-rangeZ / 2, rangeZ / 2);
            Vector3 randomPoint = spawnCenter + new Vector3(rx, 0, rz);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 5.0f, NavMesh.AllAreas))
            {
                return hit.position + Vector3.up * 0.5f;
            }
        }
        return spawnCenter;
    }

    // 範囲を黄色い枠で表示
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(spawnCenter, new Vector3(rangeX, 1, rangeZ));
    }
}