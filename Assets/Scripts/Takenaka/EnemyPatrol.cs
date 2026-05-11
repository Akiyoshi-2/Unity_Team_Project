using UnityEngine;
using UnityEngine.AI; // NavMeshを使うために必要

public class EnemyPatrol : MonoBehaviour
{
    [Header("徘徊の設定")]
    public float moveSpeed = 2.0f;      // 歩く速度
    public float patrolRadius = 10.0f; // 次の目的地を探す範囲
    public float waitTime = 2.0f;      // 目的地に着いたあとの待機時間

    private NavMeshAgent agent;
    private float waitTimer;

    void Start()
    {
        // NavMeshAgentを取得
        agent = GetComponent<NavMeshAgent>();

        // 初期の移動速度を設定
        agent.speed = moveSpeed;

        // 最初の目的地を決める
        SetRandomDestination();
    }

    void Update()
    {
        // 1. 目的地に向かって移動中かチェック
        // pathPending: 経路計算中ではない
        // remainingDistance: 目的地までの残り距離がわずか
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            // 2. 目的地に着いたらタイマーを進める
            waitTimer += Time.deltaTime;

            // 3. 待機時間が経過したら次の目的地へ
            if (waitTimer >= waitTime)
            {
                SetRandomDestination();
                waitTimer = 0;
            }
        }
    }

    // ランダムな目的地を設定する関数
    void SetRandomDestination()
    {
        // 現在地から patrolRadius の範囲内でランダムな方向を決める
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
        randomDir += transform.position;

        // その場所が NavMesh（歩ける場所）の上にあるか確認し、座標を確定させる
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, patrolRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    // エディタ上で徘徊範囲を視覚化する（青い円が表示されます）
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}