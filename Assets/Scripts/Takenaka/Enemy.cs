using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// 視線ベースの追跡敵AIクラス
/// 
/// 動作仕様:
/// 1. 【視認検知】 壁越しにプレイヤーを検知しない。視野角内かつRaycastで遮蔽なしの場合のみ追跡開始
/// 2. 【視線ロスト】 プレイヤーを壁などで見失った場合、最後に見た座標まで移動を続ける
/// 3. 【到達後の判定】 最終目視地点に到達後、周囲を再スキャン。
///    プレイヤーが見えれば追跡再開、見えなければ徘徊に戻る
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    // ==================== Inspector設定 ====================

    [Header("追跡設定")]
    [SerializeField] private float detectionRange = 10f;       // 視認可能な最大距離
    [SerializeField] private float fieldOfViewAngle = 90f;     // 視野角（両側で合計この角度）
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private float chaseSpeed = 30f;
    [SerializeField] private LayerMask obstacleMask;           // 壁・障害物のレイヤー（Inspectorで設定）

    [Header("視線ロスト後の設定")]
    [SerializeField] private float searchWaitTime = 3f;        // 最終目視地点で周囲を探す待ち時間

    [Header("徘徊設定")]
    [SerializeField] private float patrolSpeed = 10f;
    [SerializeField] private float waitTimeAtPoint = 2f;
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();

    // ==================== 内部状態 ====================

    /// <summary>敵AIの状態一覧</summary>
    private enum EnemyState
    {
        Patrol,         // 通常徘徊中
        Chase,          // プレイヤーを目視して追跡中
        SearchLastPos,  // 最終目視地点へ移動・捜索中
    }

    private NavMeshAgent agent;
    private Transform playerTransform;
    private EnemyState currentState = EnemyState.Patrol;

    // 徘徊関連
    private int currentPatrolIndex = 0;
    private float patrolTimer = 0f;

    // 視線ロスト後の捜索関連
    private Vector3 lastKnownPlayerPosition;   // プレイヤーを最後に目視した座標
    private float searchTimer = 0f;            // 最終目視地点での捜索タイマー

    // ==================== Unity ライフサイクル ====================

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = stopDistance;
        agent.speed = patrolSpeed;

        // タグ「PatrolPoints」からパトロールポイントを自動取得
        patrolPoints.Clear();
        foreach (GameObject point in GameObject.FindGameObjectsWithTag("PatrolPoints"))
        {
            patrolPoints.Add(point.transform);
        }

        // プレイヤーの参照を取得
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;
    }

    void Update()
    {
        // プレイヤー参照が失われた場合（捕捉後など）は再取得を試みる
        TryRefindPlayer();

        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrolState();
                break;

            case EnemyState.Chase:
                UpdateChaseState();
                break;

            case EnemyState.SearchLastPos:
                UpdateSearchLastPosState();
                break;
        }
    }

    // ==================== 状態別更新処理 ====================

    /// <summary>徘徊状態の更新</summary>
    private void UpdatePatrolState()
    {
        agent.speed = patrolSpeed;

        // 視線が通っていればプレイヤーを発見 → 追跡へ移行
        if (CanSeePlayer())
        {
            TransitionToChase();
            return;
        }

        if (patrolPoints.Count > 0)
        {
            Patrol();
        }
    }

    /// <summary>追跡状態の更新</summary>
    private void UpdateChaseState()
    {
        agent.speed = chaseSpeed;

        if (CanSeePlayer())
        {
            // 見えている間は最終目視地点を更新し続け、プレイヤーへ向かう
            lastKnownPlayerPosition = playerTransform.position;
            agent.SetDestination(playerTransform.position);
        }
        else
        {
            // 視線が遮断された → 最終目視地点へ移行
            Debug.Log("プレイヤーを見失いました。最後の位置へ向かいます: " + lastKnownPlayerPosition);
            TransitionToSearchLastPos();
        }
    }

    /// <summary>最終目視地点への移動・捜索状態の更新</summary>
    private void UpdateSearchLastPosState()
    {
        agent.speed = chaseSpeed;

        // 移動中でも視線が通ればすぐに追跡再開
        if (CanSeePlayer())
        {
            Debug.Log("移動中にプレイヤーを再発見！追跡再開");
            TransitionToChase();
            return;
        }

        // 最終目視地点への経路計算が完了し、残距離が十分小さくなったら「到達」とみなす
        bool arrivedAtLastPos = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;

        if (arrivedAtLastPos)
        {
            // 到達後は周囲を捜索（その場で待機しながら視線チェック）
            searchTimer += Time.deltaTime;

            if (CanSeePlayer())
            {
                // 捜索中にプレイヤーを発見 → 追跡再開
                Debug.Log("最終地点付近でプレイヤーを再発見！追跡再開");
                TransitionToChase();
            }
            else if (searchTimer >= searchWaitTime)
            {
                // 捜索時間が尽きた → 諦めて徘徊に戻る
                Debug.Log("プレイヤーが見つかりませんでした。徘徊に戻ります");
                TransitionToPatrol();
            }
        }
    }

    // ==================== 状態遷移 ====================

    private void TransitionToChase()
    {
        currentState = EnemyState.Chase;
        lastKnownPlayerPosition = playerTransform.position;
        agent.speed = chaseSpeed;
        agent.SetDestination(playerTransform.position);
        Debug.Log("プレイヤーを視認！追跡開始");
    }

    private void TransitionToSearchLastPos()
    {
        currentState = EnemyState.SearchLastPos;
        searchTimer = 0f;
        agent.SetDestination(lastKnownPlayerPosition);
    }

    private void TransitionToPatrol()
    {
        currentState = EnemyState.Patrol;
        agent.speed = patrolSpeed;
        patrolTimer = 0f;

        // 最寄りのパトロールポイントへ向かう
        if (patrolPoints.Count > 0)
        {
            currentPatrolIndex = GetNearestPatrolPointIndex();
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }

    // ==================== 視線判定 ====================

    /// <summary>
    /// プレイヤーが「視野角内」かつ「壁に遮蔽されていない」かを判定する
    /// </summary>
    private bool CanSeePlayer()
    {
        // プレイヤーが存在しない場合は見えない
        if (playerTransform == null) return false;

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        // ① 距離チェック
        if (distanceToPlayer > detectionRange) return false;

        // ② 視野角チェック（敵の正面方向との角度）
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > fieldOfViewAngle / 2f) return false;

        // ③ 壁遮蔽チェック（Raycastで障害物を確認）
        // 目の高さを少し上げてRayを飛ばすことでより自然な視線判定になる
        Vector3 eyePosition = transform.position + Vector3.up * 1.0f;
        Vector3 playerCenter = playerTransform.position + Vector3.up * 1.0f;

        if (Physics.Raycast(eyePosition, (playerCenter - eyePosition).normalized,
                            out RaycastHit hit, distanceToPlayer, obstacleMask))
        {
            // 障害物に当たった → 視線が遮られている
            return false;
        }

        // すべての条件を満たした → プレイヤーを視認できる
        return true;
    }

    // ==================== 徘徊処理 ====================

    private void Patrol()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            patrolTimer += Time.deltaTime;
            if (patrolTimer >= waitTimeAtPoint)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                patrolTimer = 0f;
            }
        }
    }

    /// <summary>最寄りのパトロールポイントのインデックスを返す</summary>
    private int GetNearestPatrolPointIndex()
    {
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            float d = Vector3.Distance(transform.position, patrolPoints[i].position);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearestIndex = i;
            }
        }
        return nearestIndex;
    }

    // ==================== プレイヤー捕捉 ====================

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("プレイヤーを捕まえました！");
            Destroy(other.gameObject);
            playerTransform = null;
            TransitionToPatrol();
        }
    }

    // ==================== ユーティリティ ====================

    private void TryRefindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
    }

    // ==================== デバッグ描画 ====================

    private void OnDrawGizmosSelected()
    {
        // 検出範囲（赤いワイヤースフィア）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 視野角（黄色い扇形の境界線）
        Gizmos.color = Color.yellow;
        Vector3 leftBoundary = Quaternion.AngleAxis(-fieldOfViewAngle / 2f, Vector3.up) * transform.forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(fieldOfViewAngle / 2f, Vector3.up) * transform.forward;
        Gizmos.DrawRay(transform.position, leftBoundary * detectionRange);
        Gizmos.DrawRay(transform.position, rightBoundary * detectionRange);

        // 最終目視地点（青いスフィア）
        if (currentState == EnemyState.SearchLastPos || currentState == EnemyState.Chase)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(lastKnownPlayerPosition, 0.4f);
            Gizmos.DrawLine(transform.position, lastKnownPlayerPosition);
        }
    }
}