using UnityEngine;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 3.0f;
    public float chaseSpeed = 5.0f;
    public float rotationSpeed = 15.0f;
    public float gridSize = 2.0f;
    [Range(0, 1)]
    public float turnProbability = 0.4f;
    public int minStraightSteps = 2;

    [Header("検知設定")]
    public float detectionRange = 10.0f;
    public float killRange = 1.2f;
    public float fovAngle = 90.0f;
    public float searchWaitTime = 2.0f;

    [Header("レイヤー・タグ設定")]
    public string playerTag = "Player";
    public LayerMask wallLayer;
    public LayerMask wallByEnemyLayer;

    [Header("デバッグ")]
    public bool showVisitLevels = true;

    private enum State { Patrol, Chase, Search }
    private State currentState = State.Patrol;

    private Vector3 targetDirection;
    private Transform player;
    private Vector3 currentTargetCell;
    private Vector3 lastSeenCell;
    private int straightStepCount = 0;
    private float searchTimer = 0f;

    // 訪問レベル管理 (0～3)
    private Dictionary<Vector3, int> visitLevelMap = new Dictionary<Vector3, int>();
    private const int MAX_VISIT_LEVEL = 3;

    private LayerMask combinedMoveMask;

    void Start()
    {
        combinedMoveMask = wallLayer | wallByEnemyLayer;
        if (wallLayer == 0) wallLayer = LayerMask.GetMask("Wall");

        SnapToGrid();
        currentTargetCell = GetGridPosition(transform.position);
        targetDirection = transform.forward;

        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        if (player == null) return;

        if (Vector3.Distance(transform.position, player.position) < killRange)
        {
            Debug.Log("プレイヤーを捕らえました！");
            Destroy(player.gameObject);
            return;
        }

        bool canSee = CanSeePlayer();

        switch (currentState)
        {
            case State.Patrol:
                if (canSee) StartChase();
                else PatrolLogic();
                break;

            case State.Chase:
                if (canSee) ChaseLogic();
                else currentState = State.Search;
                break;

            case State.Search:
                if (canSee) StartChase();
                else SearchLogic();
                break;
        }
    }

    // 視界判定
    bool CanSeePlayer()
    {
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > detectionRange) return false;

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        if (angle < fovAngle * 0.5f)
        {
            if (!Physics.Linecast(transform.position + Vector3.up, player.position + Vector3.up, wallLayer))
            {
                lastSeenCell = GetGridPosition(player.position);
                return true;
            }
        }
        return false;
    }

    // 発見時のみリセット
    void StartChase()
    {
        if (currentState != State.Chase)
        {
            currentState = State.Chase;
            visitLevelMap.Clear(); // 発見した瞬間に今までの記憶をリセット
            Debug.Log("プレイヤー発見！レベルをリセットしました");
        }
    }

    // 徘徊ロジック
    void PatrolLogic()
    {
        float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell);
        if (distToTarget < 0.05f)
        {
            Vector3 currentPos = GetGridPosition(transform.position);
            AddVisitLevel(currentPos); // 移動完了時にレベル加算
            UpdateNextPatrolTarget(currentPos);
        }
        MoveTowardsTargetSafe();
    }

    // 追跡ロジック
    void ChaseLogic()
    {
        float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell);
        if (distToTarget < 0.05f)
        {
            Vector3 currentPos = GetGridPosition(transform.position);
            AddVisitLevel(currentPos); // 追跡中もレベル加算

            Vector3 playerGridPos = GetGridPosition(player.position);
            targetDirection = GetBestDirectionTowards(currentPos, playerGridPos);
            currentTargetCell = currentPos + targetDirection * gridSize;
        }
        MoveTowardsTargetSafe();
    }

    // 捜索ロジック
    void SearchLogic()
    {
        float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell);
        float distToLastSeen = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), lastSeenCell);

        if (distToLastSeen > 0.1f)
        {
            if (distToTarget < 0.05f)
            {
                Vector3 currentPos = GetGridPosition(transform.position);
                AddVisitLevel(currentPos); // 捜索中もレベル加算
                targetDirection = GetBestDirectionTowards(currentPos, lastSeenCell);
                currentTargetCell = currentPos + targetDirection * gridSize;
            }
            MoveTowardsTargetSafe();
            searchTimer = 0f;
        }
        else
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= searchWaitTime)
            {
                currentState = State.Patrol;
            }
        }
    }

    // 次の徘徊先決定
    void UpdateNextPatrolTarget(Vector3 currentPos)
    {
        Vector3 fwd = targetDirection;
        Vector3 rgt = RoundVector(Quaternion.Euler(0, 90, 0) * fwd);
        Vector3 lft = RoundVector(Quaternion.Euler(0, -90, 0) * fwd);
        Vector3 bck = RoundVector(-fwd);

        bool canFwd = !Physics.CheckSphere(currentPos + fwd * gridSize + Vector3.up, 0.5f, combinedMoveMask);
        bool canRgt = !Physics.CheckSphere(currentPos + rgt * gridSize + Vector3.up, 0.5f, combinedMoveMask);
        bool canLft = !Physics.CheckSphere(currentPos + lft * gridSize + Vector3.up, 0.5f, combinedMoveMask);

        List<Vector3> sides = new List<Vector3>();
        if (canRgt) sides.Add(rgt);
        if (canLft) sides.Add(lft);

        if (!canFwd)
        {
            if (sides.Count > 0) targetDirection = SortByLevelPriority(sides);
            else targetDirection = bck;
            straightStepCount = 0;
        }
        else if (sides.Count > 0 && straightStepCount >= minStraightSteps)
        {
            Vector3 bestSide = SortByLevelPriority(sides);
            if (GetVisitLevel(currentPos + bestSide * gridSize) < GetVisitLevel(currentPos + fwd * gridSize) || Random.value < turnProbability)
            {
                targetDirection = bestSide;
                straightStepCount = 0;
            }
            else straightStepCount++;
        }
        else straightStepCount++;

        currentTargetCell = currentPos + targetDirection * gridSize;
    }

    // 目標への最短グリッド方向取得
    Vector3 GetBestDirectionTowards(Vector3 currentGrid, Vector3 targetGrid)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        Vector3 bestDir = targetDirection;
        float minTargetDist = float.MaxValue;

        foreach (Vector3 d in dirs)
        {
            if (!Physics.CheckSphere(currentGrid + d * gridSize + Vector3.up, 0.5f, combinedMoveMask))
            {
                float dToT = Vector3.Distance(currentGrid + d * gridSize, targetGrid);
                if (dToT < minTargetDist)
                {
                    minTargetDist = dToT;
                    bestDir = d;
                }
            }
        }
        return bestDir;
    }

    // 安全な移動処理
    void MoveTowardsTargetSafe()
    {
        // 状態に合わせてスピードを切り替える
        // 追跡中(Chase)は chaseSpeed、それ以外は moveSpeed を使う
        float currentSpeed = (currentState == State.Chase) ? chaseSpeed : moveSpeed;

        // 追跡中は回転も少し鋭くする（オプション）
        float currentRotationSpeed = (currentState == State.Chase) ? rotationSpeed * 1.5f : rotationSpeed;

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, currentRotationSpeed * 100f * Time.deltaTime);
        }

        // currentSpeed を使って移動
        Vector3 nextPos = Vector3.MoveTowards(transform.position, new Vector3(currentTargetCell.x, transform.position.y, currentTargetCell.z), currentSpeed * Time.deltaTime);

        if (!Physics.SphereCast(transform.position + Vector3.up, 0.4f, (nextPos - transform.position).normalized, out _, Vector3.Distance(transform.position, nextPos), combinedMoveMask))
        {
            transform.position = nextPos;
        }
        else
        {
            SnapToGrid();
        }
    }

    // プレイヤー接触
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            Destroy(collision.gameObject);
        }
    }

    // レベル管理
    void AddVisitLevel(Vector3 pos)
    {
        if (visitLevelMap.ContainsKey(pos))
        {
            if (visitLevelMap[pos] < MAX_VISIT_LEVEL) visitLevelMap[pos]++;
        }
        else visitLevelMap[pos] = 1;
    }

    int GetVisitLevel(Vector3 pos) => visitLevelMap.ContainsKey(pos) ? visitLevelMap[pos] : 0;

    // 可視化デバッグ
    void OnDrawGizmos()
    {
        if (!showVisitLevels || !Application.isPlaying) return;

        foreach (var entry in visitLevelMap)
        {
            int level = entry.Value;
            Color c = Color.cyan; // Level 1
            if (level == 2) c = Color.blue;
            if (level >= 3) c = Color.magenta;

            c.a = 0.4f;
            Gizmos.color = c;
            Gizmos.DrawCube(entry.Key + Vector3.up * 0.1f, new Vector3(gridSize * 0.9f, 0.1f, gridSize * 0.9f));
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(currentTargetCell + Vector3.up * 0.1f, new Vector3(gridSize, 0.2f, gridSize));
    }

    // ヘルパー
    Vector3 RoundVector(Vector3 v) => new Vector3(Mathf.Round(v.x), 0, Mathf.Round(v.z)).normalized;
    Vector3 GetGridPosition(Vector3 pos) => new Vector3(Mathf.Round(pos.x / gridSize) * gridSize, 0, Mathf.Round(pos.z / gridSize) * gridSize);
    void SnapToGrid() { Vector3 g = GetGridPosition(transform.position); transform.position = new Vector3(g.x, transform.position.y, g.z); }
    Vector3 SortByLevelPriority(List<Vector3> options)
    {
        Vector3 cur = GetGridPosition(transform.position);
        for (int i = 0; i < options.Count; i++) { Vector3 t = options[i]; int r = Random.Range(i, options.Count); options[i] = options[r]; options[r] = t; }
        Vector3 best = options[0]; int min = 99;
        foreach (Vector3 d in options) { int v = GetVisitLevel(cur + d * gridSize); if (v < min) { min = v; best = d; } }
        return best;
    }
}