using UnityEngine;
using UnityEngine.AI;

public class EnemyPatrol : MonoBehaviour
{
    [Header("徘徊の設定")]
    public float moveSpeed = 2.0f;
    public float patrolRadius = 15.0f;
    public float waitTime = 2.0f;

    [Header("追跡の設定")]
    public float detectionRange = 15.0f;
    public float chaseSpeed = 5.0f;
    [Tooltip("この距離（メートル）まで近づいたら捕まったとみなす")]
    public float catchDistance = 1.0f;

    private NavMeshAgent agent;
    private float waitTimer;
    private Transform player;
    private bool isChasing = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        // シーン内のPlayerタグを探す
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        SetRandomDestination();
    }

    void Update()
    {
        // プレイヤーが既に削除されている（null）なら何もしない
        if (player == null)
        {
            if (isChasing) StopChasing();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // 距離による強制捕獲判定
        if (isChasing && distanceToPlayer <= catchDistance)
        {
            CatchPlayer(player.gameObject);
            return;
        }

        // 探知判定
        if (distanceToPlayer <= detectionRange && CanSeePlayer())
        {
            if (!isChasing) StartChasing();
        }
        else if (isChasing)
        {
            StopChasing();
        }

        // 行動実行
        if (isChasing)
        {
            agent.SetDestination(player.position);
        }
        else
        {
            PatrolBehavior();
        }
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.0f;
        Vector3 targetPos = player.position;
        Vector3 direction = (targetPos - eyePos).normalized;

        RaycastHit hit;
        Debug.DrawRay(eyePos, direction * detectionRange, Color.red);

        if (Physics.Raycast(eyePos, direction, out hit, detectionRange))
        {
            if (hit.collider.transform.root.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    void StartChasing()
    {
        isChasing = true;
        agent.speed = chaseSpeed;
        agent.velocity = Vector3.zero;
        agent.ResetPath();
        Debug.Log("<color=yellow>★追跡開始！</color>");
    }

    void StopChasing()
    {
        isChasing = false;
        agent.speed = moveSpeed;
        Debug.Log("<color=white>▶徘徊モード</color>");
        SetRandomDestination();
    }

    // 接触判定（物理イベント）
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
        {
            CatchPlayer(other.transform.root.gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.root.CompareTag("Player"))
        {
            CatchPlayer(collision.transform.root.gameObject);
        }
    }

    // --- プレイヤー捕獲（削除）の共通処理 ---
    void CatchPlayer(GameObject target)
    {
        // 既に削除されていたら何もしない
        if (target == null) return;

        Debug.Log("<color=red>【捕獲成功】プレイヤーを削除（Destroy）しました！</color>");

        // Playerタグが付いているオブジェクト（ルート）を完全に削除
        Destroy(target.transform.root.gameObject);

        // プレイヤーがいなくなったので追跡を強制終了
        isChasing = false;
        agent.speed = moveSpeed;
        SetRandomDestination();
    }

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
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }
}