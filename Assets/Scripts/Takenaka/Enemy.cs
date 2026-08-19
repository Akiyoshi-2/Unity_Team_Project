using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public class Enemy : MonoBehaviour
{
    // ... (既存の変数はそのまま維持) ...
    [Header("移動設定")]
    public float moveSpeed = 3.0f;
    public float chaseSpeed = 5.0f;
    public float rotationSpeed = 15.0f;
    public float gridSize = 2.0f;
    [Range(0, 1)]
    public float turnProbability = 0.4f;
    public int minStraightSteps = 2;

    [Header("浮遊設定")]
    public float floatAmplitude = 0.25f;
    public float floatSpeed = 3.0f;
    private float startY;

    [Header("検知設定")]
    public float detectionRange = 10.0f;
    public float killRange = 1.2f;
    public float fovAngle = 90.0f;
    public float searchWaitTime = 2.0f;

    [Header("ドア破壊設定")]
    public float doorBreakTime = 2.0f;
    public float doorBreakRadius = 1.25f;
    public Vector3 doorCheckOffset = new Vector3(0, 1.0f, 0.8f);
    private float doorBreakTimer = 0f;

    [Header("レイヤー・タグ設定")]
    public string playerTag = "Player";
    public string warpPointTag = "Warp Point";
    public LayerMask wallLayer;
    public LayerMask wallByEnemyLayer;

    // --- 状態にDistracted(誘導)を追加 ---
    private enum State { Patrol, Chase, Search, Distracted }
    private State currentState = State.Patrol;

    private Vector3 targetDirection;
    private Transform player;
    private Vector3 currentTargetCell;
    private Vector3 lastSeenCell;
    private int straightStepCount = 0;
    private float searchTimer = 0f;

    private Dictionary<Vector3, int> visitLevelMap = new Dictionary<Vector3, int>();
    private const int MAX_VISIT_LEVEL = 3;
    private LayerMask combinedMoveMask;

    [SerializeField] private bool showVisitLevels = true;

    [SerializeField] private PostProcessVolume volume = null;
    private Grain grain;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;

    [NonSerialized] public bool flashLightHit = false;
    private bool stunFlg = false;
    private float saveChaseSpeed = 0f;
    private float saveMoveSpeed = 0f;
    private float saveDetectionRange = 0f;
    private Collider enemyCollider; // コライダー制御用

    [Header("ワープ発動条件")]
    public bool useLevelLoopWarp = true;
    public int warpThreshold = 5;
    public bool useTimeStuckWarp = true;
    public float stuckWarpTime = 10.0f;
    private int consecutiveMaxLevelCount = 0;
    private float stuckTimer = 0f;

    [Header("ノイズ演出設定")]
    [SerializeField] private float glitchDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float stretchIntensity = 2.0f;
    [SerializeField] private float glitchCooldown = 5.0f;
    private float lastGlitchTime = -999f;

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
        startY = transform.position.y;
        currentTargetCell = GetGridPosition(transform.position);
        targetDirection = transform.forward;

        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null) player = playerObj.transform;

        saveChaseSpeed = chaseSpeed;
        saveMoveSpeed = moveSpeed;
        saveDetectionRange = detectionRange;

    }

    void Update()
    {
        if (player == null) return;

        // フラッシュライト中の停止・当たり判定無効化
        HandleFlashlightStun();

        // スタン中は以下の行動処理を行わない
        if (stunFlg) return;

        // プレイヤー殺害判定（コライダーが無効な時は実行されないようにする）
        if (enemyCollider.enabled && Vector3.Distance(transform.position, player.position) < killRange)
        {
            // Playerスクリプト内の処理に合わせて調整（破壊orゲームオーバー処理）
            Destroy(player.gameObject);
            return;
        }

        if (CheckAndBreakDoor()) return;

        bool canSee = CanSeePlayer();

        // --- 時計（デコイ）のチェック ---
        if (Player.Instance != null && Player.Instance.clockFlg)
        {
            if (currentState != State.Distracted)
            {
                currentState = State.Distracted;
            }
        }

        // スタックタイマー等の更新
        if (canSee) { consecutiveMaxLevelCount = 0; stuckTimer = 0f; }
        else if (useTimeStuckWarp)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckWarpTime) WarpToNearestTaggedPoint();
        }

        // 状態遷移
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
            case State.Distracted:
                DistractedLogic();
                break;
        }
    }

    // --- 時計に引き寄せられるロジック ---
    void DistractedLogic()
    {
        // 時計フラグが消えたらパトロールに戻る
        if (Player.Instance == null || !Player.Instance.clockFlg)
        {
            currentState = State.Patrol;
            return;
        }

        Vector3 clockTarget = Player.Instance.GetClockPos();
        Vector3 clockGrid = GetGridPosition(clockTarget);

        // 現在の座標と目標セル（1歩先）の距離をチェック
        float dTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                         new Vector3(currentTargetCell.x, 0, currentTargetCell.z));

        // 次の1歩を決めるタイミング（目的地に到達したとき）
        if (dTarget < 0.1f)
        {
            Vector3 p = GetGridPosition(transform.position);

            // 時計の場所に完全に到着したか
            if (Vector3.Distance(p, clockGrid) < 0.1f)
            {
                Player.Instance.clockFlg = false;
                currentState = State.Search;
                return;
            }

            // 次に進むべき「壁のない、かつ時計に近い方向」を計算
            Vector3 nextDir = GetBestDirectionTowards(p, clockGrid);

            // もし全方向が壁で進めない場合はその場で待機（貫通防止）
            if (nextDir == Vector3.zero)
            {
                targetDirection = transform.forward; // 動かない
            }
            else
            {
                targetDirection = nextDir;
                currentTargetCell = p + targetDirection * gridSize;
            }
        }

        // 実際の移動処理
        MoveTowardsTargetSafe();
    }

    // --- フラッシュライト対応（停止と当たり判定消失） ---
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

                // 当たり判定を消す（トリガーにするか、完全に無効化するか）
                if (enemyCollider != null) enemyCollider.enabled = false;
            }
            chaseSpeed = 0;
            moveSpeed = 0;
            detectionRange = 0;
            // 物理速度もゼロにする
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }
        else if (!flashLightHit && stunFlg)
        {
            chaseSpeed = saveChaseSpeed;
            moveSpeed = saveMoveSpeed;
            detectionRange = saveDetectionRange;
            stunFlg = false;

            // 当たり判定を戻す
            if (enemyCollider != null) enemyCollider.enabled = true;
        }
    }


    void WarpToNearestTaggedPoint()
    {
        GameObject[] points = GameObject.FindGameObjectsWithTag(warpPointTag);
        if (points == null || points.Length == 0) return;
        GameObject nearestPoint = null;
        float minDistance = float.MaxValue;
        foreach (GameObject p in points)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < minDistance) { minDistance = dist; nearestPoint = p; }
        }
        if (nearestPoint != null)
        {
            transform.position = new Vector3(nearestPoint.transform.position.x, nearestPoint.transform.position.y, nearestPoint.transform.position.z);
            startY = transform.position.y;
            SnapToGrid();
            currentTargetCell = GetGridPosition(transform.position);
            currentState = State.Patrol;
            lastSeenCell = new Vector3(9999f, 9999f, 9999f);
            searchTimer = 0f;
            stuckTimer = 0f;
            consecutiveMaxLevelCount = 0;
            visitLevelMap.Clear();
            ChooseNewRandomDirection();
            StartCoroutine(PlayHardGlitch());
        }
    }

    void ChooseNewRandomDirection()
    {
        Vector3 currentPos = GetGridPosition(transform.position);
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        List<Vector3> validDirections = new List<Vector3>();
        foreach (Vector3 d in dirs)
        {
            if (!Physics.CheckSphere(currentPos + d * gridSize + Vector3.up, 0.5f, combinedMoveMask))
                validDirections.Add(d);
        }
        if (validDirections.Count > 0)
            targetDirection = validDirections[UnityEngine.Random.Range(0, validDirections.Count)];
        else
            targetDirection = transform.forward;
        currentTargetCell = currentPos + targetDirection * gridSize;
    }

    void MoveTowardsTargetSafe()
    {
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z)) > gridSize * 1.5f)
        {
            currentTargetCell = GetGridPosition(transform.position);
            return;
        }
        float speed = (currentState == State.Chase) ? chaseSpeed : moveSpeed;
        float rotSpeed = (currentState == State.Chase) ? rotationSpeed * 1.5f : rotationSpeed;
        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }
        Vector3 horizontalTarget = new Vector3(currentTargetCell.x, transform.position.y, currentTargetCell.z);
        Vector3 nextPos = Vector3.MoveTowards(transform.position, horizontalTarget, speed * Time.deltaTime);
        float floatingY = startY + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        nextPos.y = floatingY;
        Vector3 moveDir = (nextPos - transform.position).normalized;
        float moveDist = Vector3.Distance(transform.position, nextPos);
        if (moveDist > 0.001f && !Physics.SphereCast(transform.position + Vector3.up, 0.3f, moveDir, out _, moveDist, combinedMoveMask))
            transform.position = nextPos;
        else if (Vector3.Distance(transform.position, horizontalTarget) < 0.05f)
            transform.position = new Vector3(horizontalTarget.x, floatingY, horizontalTarget.z);
    }

    void PatrolLogic()
    {
        float d = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z));
        if (d < 0.1f)
        {
            Vector3 p = GetGridPosition(transform.position);
            AddVisitLevel(p);
            UpdateNextPatrolTarget(p);
        }
        MoveTowardsTargetSafe();
    }

    void ChaseLogic()
    {
        if (vignette != null) vignette.enabled.value = true;
        if (chromaticAberration != null) chromaticAberration.enabled.value = true;
        TriggerGlitchEffect();
        float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z));
        if (distToTarget < 0.1f)
        {
            Vector3 p = GetGridPosition(transform.position);
            AddVisitLevel(p);
            targetDirection = GetBestDirectionTowards(p, GetGridPosition(player.position));
            currentTargetCell = p + targetDirection * gridSize;
        }
        MoveTowardsTargetSafe();
    }

    void SearchLogic()
    {
        if (vignette != null) vignette.enabled.value = false;
        if (chromaticAberration != null) chromaticAberration.enabled.value = false;
        if (lastSeenCell.x > 5000f) { currentState = State.Patrol; return; }
        float dLast = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(lastSeenCell.x, 0, lastSeenCell.z));
        if (dLast > 0.1f)
        {
            float dTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z));
            if (dTarget < 0.1f)
            {
                Vector3 p = GetGridPosition(transform.position);
                AddVisitLevel(p);
                targetDirection = GetBestDirectionTowards(p, lastSeenCell);
                currentTargetCell = p + targetDirection * gridSize;
            }
            MoveTowardsTargetSafe();
            searchTimer = 0f;
        }
        else
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= searchWaitTime) currentState = State.Patrol;
        }
    }

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
        if (canRgt) sides.Add(rgt); if (canLft) sides.Add(lft);
        if (!canFwd) { if (sides.Count > 0) targetDirection = SortByLevelPriority(sides); else targetDirection = bck; straightStepCount = 0; }
        else if (sides.Count > 0 && straightStepCount >= minStraightSteps) { Vector3 best = SortByLevelPriority(sides); if (GetVisitLevel(currentPos + best * gridSize) < GetVisitLevel(currentPos + fwd * gridSize) || UnityEngine.Random.value < turnProbability) { targetDirection = best; straightStepCount = 0; } else straightStepCount++; }
        else straightStepCount++;
        currentTargetCell = currentPos + targetDirection * gridSize;
    }

    Vector3 GetBestDirectionTowards(Vector3 currentGrid, Vector3 targetGrid)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        Vector3 bestDir = Vector3.zero;
        float minDist = float.MaxValue;
        bool foundValidMove = false;

        foreach (Vector3 d in dirs)
        {
            Vector3 checkPos = currentGrid + d * gridSize;

            // 【最重要】進路上の壁チェック
            // 1. 行き先のマス自体に壁がないか (CheckSphere)
            // 2. 現在地から行き先のマスまでの間に壁がないか (Linecast)
            bool isBlocked = Physics.CheckSphere(checkPos + Vector3.up, 0.4f, combinedMoveMask) ||
                             Physics.Linecast(currentGrid + Vector3.up, checkPos + Vector3.up, combinedMoveMask);

            if (!isBlocked)
            {
                // 壁がない場合のみ、ターゲット（時計）との距離を計算
                float dist = Vector3.Distance(checkPos, targetGrid);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestDir = d;
                    foundValidMove = true;
                }
            }
        }

        // もし時計に向かう方向がすべて壁に阻まれている場合、
        // 「時計に近づけなくても、とりあえず壁のない方向」を探す（詰まり防止）
        if (!foundValidMove)
        {
            foreach (Vector3 d in dirs)
            {
                Vector3 checkPos = currentGrid + d * gridSize;
                if (!Physics.CheckSphere(checkPos + Vector3.up, 0.4f, combinedMoveMask))
                {
                    return d;
                }
            }
        }

        return bestDir;
    }
    bool CheckAndBreakDoor()
    {
        Vector3 checkCenter = transform.position + transform.right * doorCheckOffset.x + transform.up * doorCheckOffset.y + transform.forward * doorCheckOffset.z;
        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, doorBreakRadius);
        bool foundDoor = false;
        foreach (var hit in hitColliders) { if (hit.CompareTag("Door") || hit.CompareTag("wDoor")) { foundDoor = true; break; } }
        if (foundDoor) { doorBreakTimer += Time.deltaTime; if (doorBreakTimer >= doorBreakTime) { foreach (var hit in hitColliders) if (hit.CompareTag("Door") || hit.CompareTag("wDoor")) Destroy(hit.gameObject); doorBreakTimer = 0f; } return true; }
        doorBreakTimer = 0f; return false;
    }

    bool CanSeePlayer()
    {
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > detectionRange) return false;
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, dirToPlayer) < fovAngle * 0.5f)
        {
            if (!Physics.Linecast(transform.position + Vector3.up, player.position + Vector3.up, wallLayer))
            {
                lastSeenCell = GetGridPosition(player.position);
                return true;
            }
        }
        return false;
    }

    void StartChase()
    {
        if (currentState != State.Chase)
        {
            currentState = State.Chase;
            visitLevelMap.Clear();
            TriggerGlitchEffect();
        }
    }
    void TriggerGlitchEffect() { if (IsVisualClear() && Time.time >= lastGlitchTime + glitchCooldown) { StartCoroutine(PlayHardGlitch()); lastGlitchTime = Time.time; } }

    void AddVisitLevel(Vector3 pos)
    {
        pos = GetGridPosition(pos);

        if (!visitLevelMap.ContainsKey(pos))
        {
            visitLevelMap[pos] = 1;
        }
        else
        {
            visitLevelMap[pos]++;
        }

        // 最大訪問レベルを超えないようにする
        if (visitLevelMap[pos] > MAX_VISIT_LEVEL)
        {
            visitLevelMap[pos] = MAX_VISIT_LEVEL;
        }
    }

    int GetVisitLevel(Vector3 pos) => visitLevelMap.ContainsKey(pos) ? visitLevelMap[pos] : 0;
    private void OnCollisionEnter(Collision collision) 
    {
        if (enemyCollider.enabled && collision.gameObject.CompareTag(playerTag))
        {
            SceneManager.LoadScene("GameOverScene");
        }
    }
    Vector3 RoundVector(Vector3 v) => new Vector3(Mathf.Round(v.x), 0, Mathf.Round(v.z)).normalized;
    Vector3 GetGridPosition(Vector3 pos) => new Vector3(Mathf.Round(pos.x / gridSize) * gridSize, 0, Mathf.Round(pos.z / gridSize) * gridSize);
    void SnapToGrid() { Vector3 g = GetGridPosition(transform.position); transform.position = new Vector3(g.x, transform.position.y, g.z); }
    Vector3 SortByLevelPriority(List<Vector3> options) { Vector3 cur = GetGridPosition(transform.position); for (int i = 0; i < options.Count; i++) { Vector3 t = options[i]; int r = UnityEngine.Random.Range(i, options.Count); options[i] = options[r]; options[r] = t; } Vector3 best = options[0]; int min = 99; foreach (Vector3 d in options) { int v = GetVisitLevel(cur + d * gridSize); if (v < min) { min = v; best = d; } } return best; }
    bool IsVisualClear() { if (player == null) return false; return !Physics.Linecast(transform.position + Vector3.up, player.position + Vector3.up, combinedMoveMask); }

    void OnDrawGizmos()
    {
        if (showVisitLevels && Application.isPlaying)
        {
            foreach (var entry in visitLevelMap)
            {
                int level = entry.Value;
                Color c = Color.cyan; if (level == 2) c = Color.blue; if (level >= 3) c = Color.magenta;
                c.a = 0.2f; Gizmos.color = c; Gizmos.DrawCube(entry.Key + Vector3.up * 0.05f, new Vector3(gridSize * 0.9f, 0.1f, gridSize * 0.9f));
            }
        }
        Gizmos.color = Color.red; DrawGizmoCircle(transform.position + Vector3.up * 0.2f, killRange);
        Vector3 eyePos = transform.position + Vector3.up;
        Gizmos.color = (currentState == State.Chase) ? Color.red : (currentState == State.Search ? new Color(1f, 0.5f, 0f) : (currentState == State.Distracted ? Color.white : Color.yellow));
        Vector3 left = Quaternion.Euler(0, -fovAngle * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, fovAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(eyePos, left * detectionRange); Gizmos.DrawRay(eyePos, right * detectionRange);
    }

    void DrawGizmoCircle(Vector3 center, float radius) { int segments = 20; float step = 360f / segments; Vector3 prev = center + new Vector3(radius, 0, 0); for (int i = 1; i <= segments; i++) { float a = i * step * Mathf.Deg2Rad; Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius); Gizmos.DrawLine(prev, next); prev = next; } }

    IEnumerator PlayHardGlitch()
    {
        Camera cam = Camera.main; if (cam == null) yield break;
        float originalAspect = cam.aspect; float originalFOV = cam.fieldOfView; Vector3 originalPos = cam.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < glitchDuration)
        {
            cam.transform.localPosition = originalPos + UnityEngine.Random.insideUnitSphere * shakeIntensity;
            cam.aspect = originalAspect * UnityEngine.Random.Range(1f / stretchIntensity, stretchIntensity);
            cam.fieldOfView = originalFOV + UnityEngine.Random.Range(-15f, 15f);
            if (grain != null) grain.enabled.value = true; elapsed += Time.unscaledDeltaTime; yield return null;
        }
        cam.ResetAspect(); if (grain != null) grain.enabled.value = false; cam.fieldOfView = originalFOV; cam.transform.localPosition = originalPos;
    }
}