using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public class Enemy : MonoBehaviour
{
    // =========================================================
    // 移動設定
    // =========================================================
    [Header("移動設定")]
    public float moveSpeed = 3.0f;
    public float chaseSpeed = 5.0f;
    public float rotationSpeed = 15.0f;
    public float gridSize = 2.0f;

    [Range(0, 1)]
    public float turnProbability = 0.4f;

    public int minStraightSteps = 2;


    // =========================================================
    // 浮遊設定
    // =========================================================
    [Header("浮遊設定")]
    public float floatAmplitude = 0.25f;
    public float floatSpeed = 3.0f;

    private float startY;


    // =========================================================
    // スポーン地点への帰還設定
    // =========================================================
    [Header("スポーン地点への帰還設定")]

    // EnemyTPAreaを使用するか
    public bool useReturnToSpawn = true;

    // スポーンした場所
    private Vector3 spawnPosition;

    // スポーン地点に戻ったと判定する距離
    public float returnArrivalDistance = 0.1f;


    // =========================================================
    // 検知設定
    // =========================================================
    [Header("検知設定")]
    public float detectionRange = 10.0f;
    public float killRange = 1.2f;
    public float fovAngle = 90.0f;
    public float searchWaitTime = 2.0f;


    // =========================================================
    // ドア破壊設定
    // =========================================================
    [Header("ドア破壊設定")]
    public float doorBreakTime = 2.0f;
    public float doorBreakRadius = 1.25f;
    public Vector3 doorCheckOffset = new Vector3(0, 1.0f, 0.8f);

    private float doorBreakTimer = 0f;


    // =========================================================
    // レイヤー・タグ設定
    // =========================================================
    [Header("レイヤー・タグ設定")]
    public string playerTag = "Player";
    public string warpPointTag = "Warp Point";

    public LayerMask wallLayer;
    public LayerMask wallByEnemyLayer;


    // =========================================================
    // 状態
    // =========================================================
    // Returning = スポーン地点へ帰還中
    private enum State
    {
        Patrol,
        Chase,
        Search,
        Distracted,
        Returning
    }

    private State currentState = State.Patrol;


    // =========================================================
    // 基本変数
    // =========================================================
    private Vector3 targetDirection;
    private Transform player;

    private Vector3 currentTargetCell;
    private Vector3 lastSeenCell;

    private int straightStepCount = 0;
    private float searchTimer = 0f;


    // =========================================================
    // 訪問レベル
    // =========================================================
    private Dictionary<Vector3, int> visitLevelMap =
        new Dictionary<Vector3, int>();

    private const int MAX_VISIT_LEVEL = 3;

    private LayerMask combinedMoveMask;

    [SerializeField]
    private bool showVisitLevels = true;


    // =========================================================
    // ポストプロセス
    // =========================================================
    [SerializeField]
    private PostProcessVolume volume = null;

    private Grain grain;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;


    // =========================================================
    // フラッシュライト
    // =========================================================
    [NonSerialized]
    public bool flashLightHit = false;

    private bool stunFlg = false;

    private float saveChaseSpeed = 0f;
    private float saveMoveSpeed = 0f;
    private float saveDetectionRange = 0f;

    private Collider enemyCollider;


    // =========================================================
    // ワープ設定
    // =========================================================
    [Header("ワープ発動条件")]
    public bool useLevelLoopWarp = true;
    public int warpThreshold = 5;

    public bool useTimeStuckWarp = true;
    public float stuckWarpTime = 10.0f;

    private int consecutiveMaxLevelCount = 0;
    private float stuckTimer = 0f;

    private Vector3 lastStuckCheckPosition;


    // =========================================================
    // ノイズ演出
    // =========================================================
    [Header("ノイズ演出設定")]
    [SerializeField] private float glitchDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float stretchIntensity = 2.0f;
    [SerializeField] private float glitchCooldown = 5.0f;

    private float lastGlitchTime = -999f;


    // =========================================================
    // Start
    // =========================================================
    void Start()
    {
        // Player.InstanceがセットされるようにPlayer側のAwakeなどで Instance = this; が必要です
        enemyCollider = GetComponent<Collider>();

        if (volume != null)
        {
            volume.profile.TryGetSettings(out grain);
            volume.profile.TryGetSettings(out vignette);
            volume.profile.TryGetSettings(out chromaticAberration);
        }

        combinedMoveMask = wallLayer | wallByEnemyLayer;

        SnapToGrid();

        // ★ このEnemyが生成された場所を記憶
        spawnPosition = transform.position;

        startY = transform.position.y;

        currentTargetCell = GetGridPosition(transform.position);
        targetDirection = transform.forward;

        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObj != null)
            player = playerObj.transform;

        saveChaseSpeed = chaseSpeed;
        saveMoveSpeed = moveSpeed;
        saveDetectionRange = detectionRange;

        lastStuckCheckPosition = transform.position;
    }


    // =========================================================
    // Update
    // =========================================================
    void Update()
    {
        if (player == null) return;

        // フラッシュライト中の停止・当たり判定無効化
        HandleFlashlightStun();

        // スタン中は以下の行動処理を行わない
        if (stunFlg) return;

        // プレイヤー殺害判定（コライダーが無効な時は実行されないようにする）
        if (enemyCollider != null && enemyCollider.enabled)
        {
            Vector3 localPlayerPos =
                transform.InverseTransformPoint(player.position);

            if (Mathf.Abs(localPlayerPos.x) < killRange &&
                Mathf.Abs(localPlayerPos.z) < killRange)
            {
                Destroy(player.gameObject);
                return;
            }
        }

        // ドア破壊
        if (CheckAndBreakDoor()) return;

        bool canSee = CanSeePlayer();

        // 時計（デコイ）のチェック
        if (Player.Instance != null && Player.Instance.clockFlg)
        {
            if (currentState != State.Distracted &&
                currentState != State.Returning)
            {
                currentState = State.Distracted;
            }
        }

        // 状態遷移
        switch (currentState)
        {
            case State.Patrol:

                if (canSee)
                    StartChase();
                else
                    PatrolLogic();

                break;


            case State.Chase:

                if (canSee)
                    ChaseLogic();
                else
                    currentState = State.Search;

                break;


            case State.Search:

                if (canSee)
                    StartChase();
                else
                    SearchLogic();

                break;


            case State.Distracted:

                DistractedLogic();

                break;


            case State.Returning:

                // 帰還状態
                // EnemyTPAreaからワープする場合は
                // 基本的にこの状態は使用しません。
                break;
        }

        // 実際に動いているかチェック
        if (useTimeStuckWarp &&
            currentState != State.Returning)
        {
            Vector3 currentPos = transform.position;

            Vector3 currentHorizontalPos = new Vector3(
                currentPos.x,
                0f,
                currentPos.z
            );

            Vector3 lastHorizontalPos = new Vector3(
                lastStuckCheckPosition.x,
                0f,
                lastStuckCheckPosition.z
            );

            float movedDistance = Vector3.Distance(
                currentHorizontalPos,
                lastHorizontalPos
            );

            if (movedDistance < 0.01f)
            {
                stuckTimer += Time.deltaTime;

                if (stuckTimer >= stuckWarpTime)
                {
                    WarpToNearestTaggedPoint();

                    stuckTimer = 0f;

                    lastStuckCheckPosition =
                        transform.position;
                }
            }
            else
            {
                stuckTimer = 0f;

                lastStuckCheckPosition =
                    transform.position;
            }
        }
    }


    public void TeleportToSpawn()
    {
        transform.position = spawnPosition;

        currentTargetCell =
            GetGridPosition(spawnPosition);

        currentState = State.Patrol;

        targetDirection =
            transform.forward;

        straightStepCount = 0;
        searchTimer = 0f;
        stuckTimer = 0f;
        doorBreakTimer = 0f;

        lastSeenCell = new Vector3(
            9999f,
            9999f,
            9999f
        );

        visitLevelMap.Clear();

        startY = spawnPosition.y;

        lastStuckCheckPosition =
            transform.position;
    }
    // =========================================================
    // EnemyTPAreaから呼び出す関数
    // =========================================================
    public void ReturnToSpawn()
    {
        if (!useReturnToSpawn)
            return;

        // すでに帰還中なら何もしない
        if (currentState == State.Returning)
            return;

        StartReturningToSpawn();
    }


    // =========================================================
    // 帰還開始
    // =========================================================
    void StartReturningToSpawn()
    {
        currentState = State.Returning;

        searchTimer = 0f;

        lastSeenCell =
            new Vector3(
                9999f,
                9999f,
                9999f
            );

        doorBreakTimer = 0f;

        straightStepCount = 0;

        Vector3 currentGrid =
            GetGridPosition(transform.position);

        targetDirection =
            GetBestDirectionTowards(
                currentGrid,
                GetGridPosition(spawnPosition)
            );

        if (targetDirection != Vector3.zero)
        {
            currentTargetCell =
                currentGrid +
                targetDirection * gridSize;
        }
        else
        {
            currentTargetCell = currentGrid;
        }

        // 帰還時は探索履歴をリセット
        visitLevelMap.Clear();
    }


    // =========================================================
    // スポーン地点へ戻る
    // =========================================================
    void ReturnToSpawnLogic()
    {
        Vector3 currentHorizontal =
            new Vector3(
                transform.position.x,
                0f,
                transform.position.z
            );

        Vector3 spawnHorizontal =
            new Vector3(
                spawnPosition.x,
                0f,
                spawnPosition.z
            );

        float distance =
            Vector3.Distance(
                currentHorizontal,
                spawnHorizontal
            );


        // =====================================================
        // スポーン地点に到着
        // =====================================================
        if (distance <= returnArrivalDistance)
        {
            transform.position =
                new Vector3(
                    spawnPosition.x,
                    transform.position.y,
                    spawnPosition.z
                );

            currentTargetCell =
                GetGridPosition(transform.position);

            currentState = State.Patrol;

            targetDirection =
                transform.forward;

            straightStepCount = 0;

            searchTimer = 0f;

            lastSeenCell =
                new Vector3(
                    9999f,
                    9999f,
                    9999f
                );

            visitLevelMap.Clear();

            return;
        }


        // =====================================================
        // 次のマスへ到着したか
        // =====================================================
        float targetDistance =
            Vector3.Distance(
                new Vector3(
                    transform.position.x,
                    0f,
                    transform.position.z
                ),
                new Vector3(
                    currentTargetCell.x,
                    0f,
                    currentTargetCell.z
                )
            );


        // =====================================================
        // 次の方向を決定
        // =====================================================
        if (targetDistance < 0.1f)
        {
            Vector3 currentGrid =
                GetGridPosition(transform.position);

            targetDirection =
                GetBestDirectionTowards(
                    currentGrid,
                    GetGridPosition(spawnPosition)
                );

            if (targetDirection != Vector3.zero)
            {
                currentTargetCell =
                    currentGrid +
                    targetDirection * gridSize;
            }
        }


        // =====================================================
        // 実際に移動
        // =====================================================
        MoveTowardsTargetSafe();
    }


    // =========================================================
    // 時計に引き寄せられる
    // =========================================================
    void DistractedLogic()
    {
        if (Player.Instance == null ||
            !Player.Instance.clockFlg)
        {
            currentState = State.Patrol;
            return;
        }

        Vector3 clockTarget =
            Player.Instance.GetClockPos();

        Vector3 clockGrid =
            GetGridPosition(clockTarget);

        float dTarget =
            Vector3.Distance(
                new Vector3(
                    transform.position.x,
                    0,
                    transform.position.z
                ),
                new Vector3(
                    currentTargetCell.x,
                    0,
                    currentTargetCell.z
                )
            );


        if (dTarget < 0.1f)
        {
            Vector3 p =
                GetGridPosition(transform.position);

            if (Vector3.Distance(
                    p,
                    clockGrid) < 0.1f)
            {
                Player.Instance.clockFlg = false;

                currentState = State.Search;

                return;
            }

            Vector3 nextDir =
                GetBestDirectionTowards(
                    p,
                    clockGrid
                );

            if (nextDir == Vector3.zero)
            {
                targetDirection =
                    transform.forward;
            }
            else
            {
                targetDirection = nextDir;

                currentTargetCell =
                    p +
                    targetDirection *
                    gridSize;
            }
        }

        MoveTowardsTargetSafe();
    }


    // =========================================================
    // フラッシュライト
    // =========================================================
    void HandleFlashlightStun()
    {
        if (flashLightHit)
        {
            if (!stunFlg)
            {
                saveChaseSpeed = chaseSpeed;
                saveMoveSpeed = moveSpeed;
                saveDetectionRange = detectionRange;

                stunFlg = true;

                if (enemyCollider != null)
                    enemyCollider.enabled = false;
            }

            chaseSpeed = 0;
            moveSpeed = 0;
            detectionRange = 0;

            var rb = GetComponent<Rigidbody>();

            if (rb != null)
                rb.linearVelocity = Vector3.zero;
        }
        else if (!flashLightHit && stunFlg)
        {
            chaseSpeed = saveChaseSpeed;
            moveSpeed = saveMoveSpeed;
            detectionRange = saveDetectionRange;

            stunFlg = false;

            if (enemyCollider != null)
                enemyCollider.enabled = true;
        }
    }


    // =========================================================
    // ワープ
    // =========================================================
    void WarpToNearestTaggedPoint()
    {
        GameObject[] points =
            GameObject.FindGameObjectsWithTag(
                warpPointTag
            );

        if (points == null ||
            points.Length == 0)
            return;

        GameObject nearestPoint = null;

        float minDistance =
            float.MaxValue;

        foreach (GameObject p in points)
        {
            float dist =
                Vector3.Distance(
                    transform.position,
                    p.transform.position
                );

            if (dist < minDistance)
            {
                minDistance = dist;
                nearestPoint = p;
            }
        }

        if (nearestPoint != null)
        {
            transform.position =
                new Vector3(
                    nearestPoint.transform.position.x,
                    nearestPoint.transform.position.y,
                    nearestPoint.transform.position.z
                );

            startY =
                transform.position.y;

            SnapToGrid();

            currentTargetCell =
                GetGridPosition(transform.position);

            currentState =
                State.Patrol;

            lastSeenCell =
                new Vector3(
                    9999f,
                    9999f,
                    9999f
                );

            searchTimer = 0f;

            stuckTimer = 0f;

            consecutiveMaxLevelCount = 0;

            visitLevelMap.Clear();

            ChooseNewRandomDirection();
        }
    }


    // =========================================================
    // ランダム方向
    // =========================================================
    void ChooseNewRandomDirection()
    {
        Vector3 currentPos =
            GetGridPosition(transform.position);

        Vector3[] dirs =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left
        };

        List<Vector3> validDirections =
            new List<Vector3>();

        foreach (Vector3 d in dirs)
        {
            if (!Physics.CheckSphere(
                    currentPos +
                    d * gridSize +
                    Vector3.up,
                    0.5f,
                    combinedMoveMask))
            {
                validDirections.Add(d);
            }
        }

        if (validDirections.Count > 0)
        {
            targetDirection =
                validDirections[
                    UnityEngine.Random.Range(
                        0,
                        validDirections.Count
                    )
                ];
        }
        else
        {
            targetDirection =
                transform.forward;
        }

        currentTargetCell =
            currentPos +
            targetDirection *
            gridSize;
    }


    // =========================================================
    // 移動
    // =========================================================
    void MoveTowardsTargetSafe()
    {
        if (Vector3.Distance(
                new Vector3(
                    transform.position.x,
                    0,
                    transform.position.z
                ),
                new Vector3(
                    currentTargetCell.x,
                    0,
                    currentTargetCell.z
                )
            ) > gridSize * 1.5f)
        {
            currentTargetCell =
                GetGridPosition(transform.position);

            return;
        }

        float speed =
            (currentState == State.Chase)
            ? chaseSpeed
            : moveSpeed;

        float rotSpeed =
            (currentState == State.Chase)
            ? rotationSpeed * 1.5f
            : rotationSpeed;

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRot =
                Quaternion.LookRotation(
                    targetDirection
                );

            transform.rotation =
                Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    rotSpeed *
                    100f *
                    Time.deltaTime
                );
        }

        Vector3 horizontalTarget =
            new Vector3(
                currentTargetCell.x,
                transform.position.y,
                currentTargetCell.z
            );

        Vector3 nextPos =
            Vector3.MoveTowards(
                transform.position,
                horizontalTarget,
                speed * Time.deltaTime
            );

        float floatingY =
            startY +
            Mathf.Sin(
                Time.time *
                floatSpeed
            ) *
            floatAmplitude;

        nextPos.y = floatingY;

        Vector3 moveDir =
            (nextPos -
             transform.position).normalized;

        float moveDist =
            Vector3.Distance(
                transform.position,
                nextPos
            );

        if (moveDist > 0.001f &&
            !Physics.SphereCast(
                transform.position +
                Vector3.up,
                0.3f,
                moveDir,
                out _,
                moveDist,
                combinedMoveMask))
        {
            transform.position =
                nextPos;
        }
        else if (
            Vector3.Distance(
                transform.position,
                horizontalTarget
            ) < 0.05f)
        {
            transform.position =
                new Vector3(
                    horizontalTarget.x,
                    floatingY,
                    horizontalTarget.z
                );
        }
    }


    // =========================================================
    // パトロール
    // =========================================================
    void PatrolLogic()
    {
        float d =
            Vector3.Distance(
                new Vector3(
                    transform.position.x,
                    0,
                    transform.position.z
                ),
                new Vector3(
                    currentTargetCell.x,
                    0,
                    currentTargetCell.z
                )
            );

        if (d < 0.1f)
        {
            Vector3 p =
                GetGridPosition(transform.position);

            AddVisitLevel(p);

            UpdateNextPatrolTarget(p);
        }

        MoveTowardsTargetSafe();
    }


    // =========================================================
    // 追跡
    // =========================================================
    void ChaseLogic()
    {
        if (vignette != null)
            vignette.enabled.value = true;

        if (chromaticAberration != null)
            chromaticAberration.enabled.value = true;

        float distToTarget =
            Vector3.Distance(
                new Vector3(
                    transform.position.x,
                    0,
                    transform.position.z
                ),
                new Vector3(
                    currentTargetCell.x,
                    0,
                    currentTargetCell.z
                )
            );

        if (distToTarget < 0.1f)
        {
            Vector3 p =
                GetGridPosition(transform.position);

            AddVisitLevel(p);

            targetDirection =
                GetBestDirectionTowards(
                    p,
                    GetGridPosition(
                        player.position
                    )
                );

            if (targetDirection != Vector3.zero)
            {
                currentTargetCell =
                    p +
                    targetDirection *
                    gridSize;
            }
        }

        MoveTowardsTargetSafe();
    }


    // =========================================================
    // 探索
    // =========================================================
    void SearchLogic()
    {
        if (vignette != null)
            vignette.enabled.value = false;

        if (chromaticAberration != null)
            chromaticAberration.enabled.value = false;

        if (lastSeenCell.x > 5000f)
        {
            currentState =
                State.Patrol;

            return;
        }

        float dLast =
            Vector3.Distance(
                new Vector3(
                    transform.position.x,
                    0,
                    transform.position.z
                ),
                new Vector3(
                    lastSeenCell.x,
                    0,
                    lastSeenCell.z
                )
            );

        if (dLast > 0.1f)
        {
            float dTarget =
                Vector3.Distance(
                    new Vector3(
                        transform.position.x,
                        0,
                        transform.position.z
                    ),
                    new Vector3(
                        currentTargetCell.x,
                        0,
                        currentTargetCell.z
                    )
                );

            if (dTarget < 0.1f)
            {
                Vector3 p =
                    GetGridPosition(
                        transform.position
                    );

                AddVisitLevel(p);

                targetDirection =
                    GetBestDirectionTowards(
                        p,
                        lastSeenCell
                    );

                if (targetDirection != Vector3.zero)
                {
                    currentTargetCell =
                        p +
                        targetDirection *
                        gridSize;
                }
            }

            MoveTowardsTargetSafe();

            searchTimer = 0f;
        }
        else
        {
            searchTimer +=
                Time.deltaTime;

            if (searchTimer >= searchWaitTime)
                currentState =
                    State.Patrol;
        }
    }


    // =========================================================
    // 次のパトロール地点
    // =========================================================
    void UpdateNextPatrolTarget(
        Vector3 currentPos)
    {
        Vector3 fwd =
            targetDirection;

        Vector3 rgt =
            RoundVector(
                Quaternion.Euler(
                    0,
                    90,
                    0
                ) * fwd
            );

        Vector3 lft =
            RoundVector(
                Quaternion.Euler(
                    0,
                    -90,
                    0
                ) * fwd
            );

        Vector3 bck =
            RoundVector(-fwd);

        bool canFwd =
            !Physics.CheckSphere(
                currentPos +
                fwd * gridSize +
                Vector3.up,
                0.5f,
                combinedMoveMask
            );

        bool canRgt =
            !Physics.CheckSphere(
                currentPos +
                rgt * gridSize +
                Vector3.up,
                0.5f,
                combinedMoveMask
            );

        bool canLft =
            !Physics.CheckSphere(
                currentPos +
                lft * gridSize +
                Vector3.up,
                0.5f,
                combinedMoveMask
            );

        List<Vector3> sides =
            new List<Vector3>();

        if (canRgt)
            sides.Add(rgt);

        if (canLft)
            sides.Add(lft);


        if (!canFwd)
        {
            if (sides.Count > 0)
            {
                targetDirection =
                    SortByLevelPriority(
                        sides
                    );
            }
            else
            {
                targetDirection = bck;
            }

            straightStepCount = 0;
        }
        else if (
            sides.Count > 0 &&
            straightStepCount >= minStraightSteps)
        {
            Vector3 best =
                SortByLevelPriority(
                    sides
                );

            if (
                GetVisitLevel(
                    currentPos +
                    best *
                    gridSize
                )
                <
                GetVisitLevel(
                    currentPos +
                    fwd *
                    gridSize
                )
                ||
                UnityEngine.Random.value <
                turnProbability)
            {
                targetDirection = best;

                straightStepCount = 0;
            }
            else
            {
                straightStepCount++;
            }
        }
        else
        {
            straightStepCount++;
        }

        currentTargetCell =
            currentPos +
            targetDirection *
            gridSize;
    }


    // =========================================================
    // 目的地への最適方向
    // =========================================================
    Vector3 GetBestDirectionTowards(
        Vector3 currentGrid,
        Vector3 targetGrid)
    {
        Vector3[] dirs =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left
        };

        Vector3 bestDir =
            Vector3.zero;

        float minDist =
            float.MaxValue;

        bool foundValidMove = false;

        foreach (Vector3 d in dirs)
        {
            Vector3 checkPos =
                currentGrid +
                d * gridSize;

            bool isBlocked =
                Physics.CheckSphere(
                    checkPos + Vector3.up,
                    0.4f,
                    combinedMoveMask
                )
                ||
                Physics.Linecast(
                    currentGrid + Vector3.up,
                    checkPos + Vector3.up,
                    combinedMoveMask
                );

            if (!isBlocked)
            {
                float dist =
                    Vector3.Distance(
                        checkPos,
                        targetGrid
                    );

                if (dist < minDist)
                {
                    minDist = dist;

                    bestDir = d;

                    foundValidMove = true;
                }
            }
        }


        // どの方向もターゲットに近づけない場合
        // 壁のない方向を探す
        if (!foundValidMove)
        {
            foreach (Vector3 d in dirs)
            {
                Vector3 checkPos =
                    currentGrid +
                    d * gridSize;

                if (!Physics.CheckSphere(
                        checkPos + Vector3.up,
                        0.4f,
                        combinedMoveMask))
                {
                    return d;
                }
            }
        }

        return bestDir;
    }


    // =========================================================
    // ドア破壊
    // =========================================================
    bool CheckAndBreakDoor()
    {
        Vector3 checkCenter =
            transform.position +
            transform.right *
            doorCheckOffset.x +
            transform.up *
            doorCheckOffset.y +
            transform.forward *
            doorCheckOffset.z;

        Collider[] hitColliders =
            Physics.OverlapSphere(
                checkCenter,
                doorBreakRadius
            );

        bool foundDoor = false;

        foreach (var hit in hitColliders)
        {
            if (
                hit.CompareTag("Door") ||
                hit.CompareTag("wDoor"))
            {
                foundDoor = true;
                break;
            }
        }

        if (foundDoor)
        {
            doorBreakTimer +=
                Time.deltaTime;

            if (doorBreakTimer >= doorBreakTime)
            {
                foreach (var hit in hitColliders)
                {
                    if (
                        hit.CompareTag("Door") ||
                        hit.CompareTag("wDoor"))
                    {
                        Destroy(hit.gameObject);
                    }
                }

                doorBreakTimer = 0f;
            }

            return true;
        }

        doorBreakTimer = 0f;

        return false;
    }


    // =========================================================
    // プレイヤー視認
    // =========================================================
    bool CanSeePlayer()
    {
        float dist =
            Vector3.Distance(
                transform.position,
                player.position
            );

        if (dist > detectionRange)
            return false;

        Vector3 dirToPlayer =
            (player.position -
             transform.position).normalized;

        if (
            Vector3.Angle(
                transform.forward,
                dirToPlayer
            )
            <
            fovAngle * 0.5f)
        {
            if (
                !Physics.Linecast(
                    transform.position +
                    Vector3.up,
                    player.position +
                    Vector3.up,
                    wallLayer))
            {
                lastSeenCell =
                    GetGridPosition(
                        player.position
                    );

                return true;
            }
        }

        return false;
    }


    // =========================================================
    // 追跡開始
    // =========================================================
    void StartChase()
    {
        if (currentState != State.Chase)
        {
            currentState = State.Chase;

            visitLevelMap.Clear();

            StartCoroutine(
                PlayHardGlitch()
            );
        }
    }


    // =========================================================
    // グリッチ
    // =========================================================
    void TriggerGlitchEffect()
    {
        if (
            CanSeePlayer() &&
            Time.time >=
            lastGlitchTime +
            glitchCooldown)
        {
            StartCoroutine(
                PlayHardGlitch()
            );

            lastGlitchTime =
                Time.time;
        }
    }


    // =========================================================
    // 訪問レベル
    // =========================================================
    void AddVisitLevel(Vector3 pos)
    {
        pos =
            GetGridPosition(pos);

        if (!visitLevelMap.ContainsKey(pos))
        {
            visitLevelMap[pos] = 1;
        }
        else
        {
            visitLevelMap[pos]++;
        }

        if (
            visitLevelMap[pos] >
            MAX_VISIT_LEVEL)
        {
            visitLevelMap[pos] =
                MAX_VISIT_LEVEL;
        }
    }


    int GetVisitLevel(Vector3 pos)
    {
        return visitLevelMap.ContainsKey(pos)
            ? visitLevelMap[pos]
            : 0;
    }


    // =========================================================
    // プレイヤーとの衝突
    // =========================================================
    private void OnCollisionEnter(
        Collision collision)
    {
        if (
            enemyCollider != null &&
            enemyCollider.enabled &&
            collision.gameObject.CompareTag(
                playerTag))
        {
            SceneManager.LoadScene(
                "GameOverScene"
            );
        }
    }


    // =========================================================
    // グリッド関連
    // =========================================================
    Vector3 RoundVector(Vector3 v)
    {
        return new Vector3(
            Mathf.Round(v.x),
            0,
            Mathf.Round(v.z)
        ).normalized;
    }


    Vector3 GetGridPosition(Vector3 pos)
    {
        return new Vector3(
            Mathf.Round(
                pos.x / gridSize
            ) * gridSize,

            0,

            Mathf.Round(
                pos.z / gridSize
            ) * gridSize
        );
    }


    void SnapToGrid()
    {
        Vector3 g =
            GetGridPosition(
                transform.position
            );

        transform.position =
            new Vector3(
                g.x,
                transform.position.y,
                g.z
            );
    }


    // =========================================================
    // 訪問レベルによる方向選択
    // =========================================================
    Vector3 SortByLevelPriority(
        List<Vector3> options)
    {
        Vector3 cur =
            GetGridPosition(
                transform.position
            );

        for (
            int i = 0;
            i < options.Count;
            i++)
        {
            int r =
                UnityEngine.Random.Range(
                    i,
                    options.Count
                );

            Vector3 temp =
                options[i];

            options[i] =
                options[r];

            options[r] =
                temp;
        }

        Vector3 best =
            options[0];

        int min = 99;

        foreach (Vector3 d in options)
        {
            int v =
                GetVisitLevel(
                    cur +
                    d *
                    gridSize
                );

            if (v < min)
            {
                min = v;

                best = d;
            }
        }

        return best;
    }


    // =========================================================
    // 視認チェック
    // =========================================================
    bool IsVisualClear()
    {
        if (player == null)
            return false;

        return !Physics.Linecast(
            transform.position +
            Vector3.up,

            player.position +
            Vector3.up,

            combinedMoveMask
        );
    }


    // =========================================================
    // Gizmos
    // =========================================================
    void OnDrawGizmos()
    {
        if (
            showVisitLevels &&
            Application.isPlaying)
        {
            foreach (
                var entry
                in visitLevelMap)
            {
                int level =
                    entry.Value;

                Color c =
                    Color.cyan;

                if (level == 2)
                    c = Color.blue;

                if (level >= 3)
                    c = Color.magenta;

                c.a = 0.2f;

                Gizmos.color = c;

                Gizmos.DrawCube(
                    entry.Key +
                    Vector3.up *
                    0.05f,

                    new Vector3(
                        gridSize * 0.9f,
                        0.1f,
                        gridSize * 0.9f
                    )
                );
            }
        }


        // =====================================================
        // 殺害範囲
        // =====================================================
        Gizmos.color =
            Color.red;

        Vector3 killCenter =
            transform.position +
            Vector3.up *
            0.2f;

        Vector3 killSize =
            new Vector3(
                killRange * 2f,
                0.1f,
                killRange * 2f
            );

        Matrix4x4 oldMatrix =
            Gizmos.matrix;

        Gizmos.matrix =
            Matrix4x4.TRS(
                killCenter,
                transform.rotation,
                Vector3.one
            );

        Gizmos.DrawWireCube(
            Vector3.zero,
            killSize
        );

        Gizmos.matrix =
            oldMatrix;


        // =====================================================
        // 視野角
        // =====================================================
        Vector3 eyePos =
            transform.position +
            Vector3.up;

        Gizmos.color =
            (currentState == State.Chase)
            ? Color.red
            : (
                currentState == State.Search
                ? new Color(
                    1f,
                    0.5f,
                    0f
                )
                : (
                    currentState ==
                    State.Distracted
                    ? Color.white
                    : (
                        currentState ==
                        State.Returning
                        ? Color.magenta
                        : Color.yellow
                    )
                )
            );

        Vector3 left =
            Quaternion.Euler(
                0,
                -fovAngle * 0.5f,
                0
            ) *
            transform.forward;

        Vector3 right =
            Quaternion.Euler(
                0,
                fovAngle * 0.5f,
                0
            ) *
            transform.forward;

        Gizmos.DrawRay(
            eyePos,
            left *
            detectionRange
        );

        Gizmos.DrawRay(
            eyePos,
            right *
            detectionRange
        );


        // =====================================================
        // スポーン地点への帰還線
        // =====================================================
        if (
            Application.isPlaying &&
            currentState ==
            State.Returning)
        {
            Gizmos.color =
                Color.magenta;

            Gizmos.DrawLine(
                transform.position,
                spawnPosition
            );

            Gizmos.DrawWireSphere(
                spawnPosition,
                returnArrivalDistance
            );
        }
    }


    // =========================================================
    // Gizmo Circle
    // =========================================================
    void DrawGizmoCircle(
        Vector3 center,
        float radius)
    {
        int segments = 20;

        float step =
            360f /
            segments;

        Vector3 prev =
            center +
            new Vector3(
                radius,
                0,
                0
            );

        for (
            int i = 1;
            i <= segments;
            i++)
        {
            float a =
                i *
                step *
                Mathf.Deg2Rad;

            Vector3 next =
                center +
                new Vector3(
                    Mathf.Cos(a) *
                    radius,
                    0,
                    Mathf.Sin(a) *
                    radius
                );

            Gizmos.DrawLine(
                prev,
                next
            );

            prev = next;
        }
    }


    // =========================================================
    // ハードグリッチ
    // =========================================================
    IEnumerator PlayHardGlitch()
    {
        Camera cam =
            Camera.main;

        if (cam == null)
            yield break;

        float originalAspect =
            cam.aspect;

        float originalFOV =
            cam.fieldOfView;

        Vector3 originalPos =
            cam.transform.localPosition;

        float elapsed = 0f;

        while (
            elapsed <
            glitchDuration)
        {
            cam.transform.localPosition =
                originalPos +
                UnityEngine.Random.insideUnitSphere *
                shakeIntensity;

            cam.aspect =
                originalAspect *
                UnityEngine.Random.Range(
                    1f /
                    stretchIntensity,
                    stretchIntensity
                );

            cam.fieldOfView =
                originalFOV +
                UnityEngine.Random.Range(
                    -15f,
                    15f
                );

            if (grain != null)
                grain.enabled.value =
                    true;

            elapsed +=
                Time.unscaledDeltaTime;

            yield return null;
        }

        cam.ResetAspect();

        if (grain != null)
            grain.enabled.value =
                false;

        cam.fieldOfView =
            originalFOV;

        cam.transform.localPosition =
            originalPos;
    }
}