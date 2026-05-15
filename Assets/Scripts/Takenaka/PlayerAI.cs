using UnityEngine;
using UnityEngine.AI;

public class PlayerAI : MonoBehaviour
{
    [Header("徘徊設定")]
    public float walkSpeed = 2.0f;
    public float patrolRadius = 15.0f;
    public float waitTime = 3.0f;

    [Header("逃走設定")]
    public float fleeSpeed = 5.5f;     // 敵より少し速いくらいがおすすめ
    public float detectionRange = 12.0f; // 敵を警戒する距離
    public float fleeDistance = 15.0f;  // 敵から離れようとする距離

    private NavMeshAgent agent;
    private Transform enemy;
    private float waitTimer;
    private bool isFleeing = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;

        // シーン内の敵を探す（タグが"Enemy"のオブジェクトを探す）
        FindEnemy();

        SetRandomDestination();
    }

    void Update()
    {
        if (enemy == null)
        {
            FindEnemy();
            PatrolBehavior();
            return;
        }

        // 敵との距離を計算
        float distanceToEnemy = Vector3.Distance(transform.position, enemy.position);

        // --- 判定ロジック ---
        if (distanceToEnemy <= detectionRange)
        {
            // 敵が近いので逃げる
            isFleeing = true;
            FleeFromEnemy();
        }
        else
        {
            // 敵が遠い場合
            if (isFleeing)
            {
                // 逃走直後は一度立ち止まってから徘徊に戻る
                isFleeing = false;
                agent.speed = walkSpeed;
                waitTimer = 0;
            }
            PatrolBehavior();
        }
    }

    void FindEnemy()
    {
        // "Enemy"タグが付いているオブジェクトを探す
        GameObject enemyObj = GameObject.FindGameObjectWithTag("Enemy");
        if (enemyObj != null) enemy = enemyObj.transform;
    }

    // 敵から反対方向に逃げる
    void FleeFromEnemy()
    {
        agent.speed = fleeSpeed;

        // 敵から自分への方向ベクトルを計算
        Vector3 directionToPlayer = transform.position - enemy.position;

        // 逃げる目標地点（今の位置から敵の反対方向へ）
        Vector3 fleePos = transform.position + directionToPlayer.normalized * fleeDistance;

        // 目標地点がNavMeshの上にあるか確認して移動
        NavMeshHit hit;
        if (NavMesh.SamplePosition(fleePos, out hit, fleeDistance, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // 敵がいない時ののんびり徘徊
    void PatrolBehavior()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTime)
            {
                SetRandomDestination();
                waitTimer = 0;
            }
        }
    }

    void SetRandomDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // デバッグ表示（Sceneビューで確認用）
    void OnDrawGizmos()
    {
        // 警戒範囲を黄色で表示
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}