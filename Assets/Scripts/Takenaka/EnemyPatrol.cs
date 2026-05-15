using UnityEngine;
using UnityEngine.AI;

public class EnemyPatrol : MonoBehaviour
{
    [Header("徘徊")]
    public float patrolRadius = 999f;
    public float waitTime = 0f;
    public float moveSpeed = 5f;

    [Header("追跡")]
    public float detectionRange = 50f;
    public float chaseSpeed = 8f;
    public float catchDistance = 1.5f; // インスペクターより少し広めに設定

    [Header("探索")]
    public float searchTime = 5f;
    public float lostTargetGracePeriod = 1.5f;

    private NavMeshAgent agent;
    private Transform player;
    private Vector3 startPosition;
    private Vector3 lastKnownPosition;
    private float waitTimer;
    private float searchTimer;
    private float lostTargetTimer;

    private enum EnemyState { Patrol, Chase, Search }
    private EnemyState currentState;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        FindPlayer();
        startPosition = transform.position;
        currentState = EnemyState.Patrol;
        SetRandomDestination();
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        // --- 重要：距離による絶対的な捕獲判定 ---
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= catchDistance)
        {
            Debug.Log("<color=red>【距離判定】プレイヤーを捕獲！</color>");
            CatchPlayer();
            return;
        }

        bool canSeePlayer = CanSeePlayerWithLog();

        // 視界に捉えている間は常に目的地を更新
        if (canSeePlayer)
        {
            if (currentState != EnemyState.Chase)
            {
                agent.ResetPath();
                currentState = EnemyState.Chase;
                Debug.Log("<color=yellow>【視界発見】追跡中</color>");
            }
            lastKnownPosition = player.position;
            agent.SetDestination(player.position);
            lostTargetTimer = 0;
            agent.speed = chaseSpeed;
        }

        switch (currentState)
        {
            case EnemyState.Patrol:
                agent.speed = moveSpeed;
                Patrol();
                break;

            case EnemyState.Chase:
                if (!canSeePlayer)
                {
                    lostTargetTimer += Time.deltaTime;
                    if (lostTargetTimer < lostTargetGracePeriod)
                    {
                        agent.SetDestination(player.position);
                    }
                    else
                    {
                        currentState = EnemyState.Search;
                        searchTimer = searchTime;
                        agent.SetDestination(lastKnownPosition);
                        Debug.Log("<color=orange>【見失い】探索へ移行</color>");
                    }
                }
                break;

            case EnemyState.Search:
                agent.speed = moveSpeed;
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    searchTimer -= Time.deltaTime;
                    if (searchTimer <= 0) StopChasing();
                }
                break;
        }
    }

    bool CanSeePlayerWithLog()
    {
        if (player == null) return false;
        // 敵の目線をさらに高く（自分に当たらないように）
        Vector3 eyePos = transform.position + Vector3.up * 1.6f;
        Vector3 targetPos = player.position + Vector3.up * 0.5f;
        Vector3 dir = (targetPos - eyePos).normalized;

        RaycastHit hit;
        Debug.DrawRay(eyePos, dir * detectionRange, Color.red);

        // 自分を無視するために1.2m前方から発射
        if (Physics.Raycast(eyePos + dir * 1.2f, dir, out hit, detectionRange))
        {
            if (hit.collider.CompareTag("Player") || hit.transform.root.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    void CatchPlayer()
    {
        if (player == null) return;

        GameObject targetObj = player.root.gameObject;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestRespawn(targetObj);
        }
        else
        {
            Destroy(targetObj);
        }

        player = null;
        StopChasing();
        agent.velocity = Vector3.zero;
        agent.ResetPath();
    }

    // 物理的な接触でも反応するように追加
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            Debug.Log("<color=red>【物理接触】プレイヤーを捕獲！</color>");
            CatchPlayer();
        }
    }

    void FindPlayer()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        if (obj != null) player = obj.transform;
    }

    void Patrol()
    {
        if (agent.pathPending || agent.remainingDistance > 0.5f) return;
        waitTimer += Time.deltaTime;
        if (waitTimer >= waitTime) { SetRandomDestination(); waitTimer = 0; }
    }

    void SetRandomDestination()
    {
        Vector3 randomPos = startPosition + Random.insideUnitSphere * patrolRadius;
        randomPos.y = transform.position.y;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPos, out hit, 10f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void StopChasing()
    {
        currentState = EnemyState.Patrol;
        agent.speed = moveSpeed;
        agent.ResetPath();
        SetRandomDestination();
    }
}