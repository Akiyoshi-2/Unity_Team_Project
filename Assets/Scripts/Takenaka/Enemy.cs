using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// 視線ベースの追跡敵AIクラス
///
/// 動作仕様:
/// 1. 【視認検知】 壁越しにプレイヤーを検知しない。視野角内かつRaycastで遮蔽なしの場合のみ追跡開始
/// 2. 【視線ロスト】 プレイヤーを壁などで見失った場合、最後に見た座標まで移動を続ける
/// 3. 【到達後の捜索】 最終目視地点に到達後、左右に見回しながら周囲をスキャン。
///    プレイヤーが見えれば追跡再開、見えなければ徘徊に戻る
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    // ==================== Inspector設定 ====================

    [Header("追跡設定")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float fieldOfViewAngle = 90f;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private float chaseSpeed = 30f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("視線ロスト後の設定")]
    [SerializeField] private float searchWaitTime = 3f;        // 見回し含む総捜索時間
    [SerializeField] private float overshootDistance = 5f;     // 見失った後さらに進む距離

    [Header("見回し設定")]
    [SerializeField] private float lookAngle = 60f;            // 左右それぞれ何度振るか
    [SerializeField] private float lookSpeed = 60f;            // 見回し速度（度/秒）

    [Header("徘徊設定")]
    [SerializeField] private float patrolSpeed = 10f;
    [SerializeField] private float waitTimeAtPoint = 2f;
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();

    // ==================== 内部状態 ====================

    private enum EnemyState
    {
        Patrol,
        Chase,
        SearchLastPos,
    }

    // 見回しのフェーズ
    private enum LookPhase
    {
        TurnLeft,   // 左へ振る
        TurnRight,  // 右へ振る（左端 → 右端）
        TurnCenter, // 中央へ戻る
        Done,       // 完了
    }

    private NavMeshAgent agent;
    private Transform playerTransform;
    private EnemyState currentState = EnemyState.Patrol;

    // 徘徊
    private int currentPatrolIndex = 0;
    private float patrolTimer = 0f;

    // 捜索
    private Vector3 lastKnownPlayerPosition;
    private float searchTimer = 0f;

    // 見回し
    private LookPhase lookPhase = LookPhase.Done;
    private float arrivalYaw;       // 最終目視地点到達時の向き（Y軸回転）
    private float currentLookYaw;   // 現在の見回し中のYaw角度
    private float targetLookYaw;    // 現在フェーズの目標Yaw角度

    // ==================== Unity ライフサイクル ====================

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = stopDistance;
        agent.speed = patrolSpeed;

        patrolPoints.Clear();
        foreach (GameObject point in GameObject.FindGameObjectsWithTag("PatrolPoints"))
            patrolPoints.Add(point.transform);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;
    }

    void Update()
    {
        TryRefindPlayer();

        switch (currentState)
        {
            case EnemyState.Patrol: UpdatePatrolState(); break;
            case EnemyState.Chase: UpdateChaseState(); break;
            case EnemyState.SearchLastPos: UpdateSearchLastPosState(); break;
        }
    }

    // ==================== 状態別更新 ====================

    private void UpdatePatrolState()
    {
        agent.speed = patrolSpeed;
        if (CanSeePlayer()) { TransitionToChase(); return; }
        if (patrolPoints.Count > 0) Patrol();
    }

    private void UpdateChaseState()
    {
        agent.speed = chaseSpeed;
        if (CanSeePlayer())
        {
            lastKnownPlayerPosition = playerTransform.position;
            agent.SetDestination(playerTransform.position);
        }
        else
        {
            Debug.Log("プレイヤーを見失いました。最後の位置へ向かいます: " + lastKnownPlayerPosition);
            TransitionToSearchLastPos();
        }
    }

    private void UpdateSearchLastPosState()
    {
        agent.speed = chaseSpeed;

        // 移動中でも視線が通れば追跡再開
        if (CanSeePlayer())
        {
            Debug.Log("移動中にプレイヤーを再発見！追跡再開");
            TransitionToChase();
            return;
        }

        bool arrivedAtLastPos = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;

        if (!arrivedAtLastPos) return; // まだ移動中

        // ── 到達後：見回し捜索フェーズ ──

        // 見回しをまだ開始していなければ初期化
        if (lookPhase == LookPhase.Done && searchTimer == 0f)
            StartLookAround();

        // 見回し中の回転処理
        if (lookPhase != LookPhase.Done)
            UpdateLookAround();

        // 総捜索時間のカウント
        searchTimer += Time.deltaTime;

        // 視線チェック（見回し中も毎フレーム判定）
        if (CanSeePlayer())
        {
            Debug.Log("捜索中にプレイヤーを再発見！追跡再開");
            TransitionToChase();
            return;
        }

        // 時間切れ → 徘徊へ
        if (searchTimer >= searchWaitTime)
        {
            Debug.Log("プレイヤーが見つかりませんでした。徘徊に戻ります");
            TransitionToPatrol();
        }
    }

    // ==================== 見回し処理 ====================

    /// <summary>見回し開始：到達時の向きを基準として左右フェーズを設定</summary>
    private void StartLookAround()
    {
        arrivalYaw = transform.eulerAngles.y;
        currentLookYaw = arrivalYaw;
        lookPhase = LookPhase.TurnLeft;
        targetLookYaw = arrivalYaw - lookAngle; // まず左へ
        agent.updateRotation = false;            // NavMeshの自動回転をOFF
    }

    /// <summary>毎フレーム呼ばれる見回し回転処理</summary>
    private void UpdateLookAround()
    {
        // 目標Yawへ向けてlookSpeedで回転
        currentLookYaw = Mathf.MoveTowards(currentLookYaw, targetLookYaw, lookSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, currentLookYaw, 0f);

        // 目標角度に到達したら次のフェーズへ
        if (Mathf.Approximately(currentLookYaw, targetLookYaw))
        {
            switch (lookPhase)
            {
                case LookPhase.TurnLeft:
                    // 左端 → 右端へ（左端から右端なので角度差は lookAngle * 2）
                    lookPhase = LookPhase.TurnRight;
                    targetLookYaw = arrivalYaw + lookAngle;
                    break;

                case LookPhase.TurnRight:
                    // 右端 → 中央へ戻る
                    lookPhase = LookPhase.TurnCenter;
                    targetLookYaw = arrivalYaw;
                    break;

                case LookPhase.TurnCenter:
                    // 見回し完了
                    lookPhase = LookPhase.Done;
                    agent.updateRotation = true; // NavMeshの自動回転を戻す
                    break;
            }
        }
    }

    /// <summary>見回し状態をリセット（他状態へ遷移するときに呼ぶ）</summary>
    private void ResetLookAround()
    {
        lookPhase = LookPhase.Done;
        agent.updateRotation = true;
    }

    // ==================== 状態遷移 ====================

    private void TransitionToChase()
    {
        ResetLookAround();
        currentState = EnemyState.Chase;
        lastKnownPlayerPosition = playerTransform.position;
        agent.speed = chaseSpeed;
        agent.SetDestination(playerTransform.position);
        Debug.Log("プレイヤーを視認！追跡開始");
    }

    private void TransitionToSearchLastPos()
    {
        ResetLookAround();
        currentState = EnemyState.SearchLastPos;
        searchTimer = 0f;

        // 見失った瞬間の敵→プレイヤー方向へ overshootDistance だけ先の地点を目標にする
        Vector3 lostDirection = (lastKnownPlayerPosition - transform.position).normalized;
        Vector3 overshootTarget = lastKnownPlayerPosition + lostDirection * overshootDistance;

        // NavMesh上の有効な地点に丸める（壁の中に入らないよう）
        if (NavMesh.SamplePosition(overshootTarget, out NavMeshHit navHit, overshootDistance, NavMesh.AllAreas))
            agent.SetDestination(navHit.position);
        else
            agent.SetDestination(lastKnownPlayerPosition); // 有効地点が取れなければ最終目視地点へ
    }

    private void TransitionToPatrol()
    {
        ResetLookAround();
        currentState = EnemyState.Patrol;
        agent.speed = patrolSpeed;
        patrolTimer = 0f;

        if (patrolPoints.Count > 0)
        {
            currentPatrolIndex = GetNearestPatrolPointIndex();
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }

    // ==================== 視線判定 ====================

    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer > detectionRange) return false;

        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > fieldOfViewAngle / 2f) return false;

        Vector3 eyePosition = transform.position + Vector3.up * 1.0f;
        Vector3 playerCenter = playerTransform.position + Vector3.up * 1.0f;

        if (Physics.Raycast(eyePosition, (playerCenter - eyePosition).normalized,
                            out RaycastHit hit, distanceToPlayer, obstacleMask))
            return false;

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

    private int GetNearestPatrolPointIndex()
    {
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < patrolPoints.Count; i++)
        {
            float d = Vector3.Distance(transform.position, patrolPoints[i].position);
            if (d < nearestDistance) { nearestDistance = d; nearestIndex = i; }
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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.AngleAxis(-fieldOfViewAngle / 2f, Vector3.up) * transform.forward;
        Vector3 right = Quaternion.AngleAxis(fieldOfViewAngle / 2f, Vector3.up) * transform.forward;
        Gizmos.DrawRay(transform.position, left * detectionRange);
        Gizmos.DrawRay(transform.position, right * detectionRange);

        if (currentState == EnemyState.SearchLastPos || currentState == EnemyState.Chase)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(lastKnownPlayerPosition, 0.4f);
            Gizmos.DrawLine(transform.position, lastKnownPlayerPosition);
        }
    }
}