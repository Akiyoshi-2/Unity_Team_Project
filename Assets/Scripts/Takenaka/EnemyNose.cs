using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class EnemyNose : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 3.0f;
    public float chaseSpeed = 5.0f;
    public float rotationSpeed = 15.0f;
    public float gridSize = 2.0f;
    [Range(0, 1)]
    public float turnProbability = 0.4f;
    public int minStraightSteps = 2;

    [Header("浮遊設定")] // ★追加
    public float floatAmplitude = 0.25f;
    public float floatSpeed = 3.0f;
    private float startY;

    [Header("検知設定")]
    public float detectionRange = 10.0f;
    public float killRange = 1.2f;
    public float fovAngle = 90.0f;
    public float searchWaitTime = 2.0f;

    [Header("レイヤー・タグ設定")]
    public string playerTag = "Player";
    public string warpPointTag = "Warp Point";
    public LayerMask wallLayer;
    public LayerMask wallByEnemyLayer;

    [Header("デバッグ")]
    public bool showVisitLevels = true;

    [Header("スタック対策設定")]
    public bool useLevelLoopWarp = true;
    public int warpThreshold = 5;
    public bool useTimeStuckWarp = true;
    public float stuckWarpTime = 10.0f;

    private int consecutiveMaxLevelCount = 0;
    private float stuckTimer = 0f;

    private enum State { Patrol, Chase, Search }
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

    [SerializeField]
    private PostProcessVolume volume = null;

    private Grain grain;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;

    [NonSerialized]
    public bool flashLightHit = false;
    private bool stunFlg = false;
    private float saveChaseSpeed = 0f;
    private float saveMoveSpeed = 0f;
    private float saveDetectionRange = 0f;

    [Header("ノイズ演出設定")]
    [SerializeField] private float glitchDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float stretchIntensity = 2.0f;
    [SerializeField] private float glitchCooldown = 5.0f;
    private float lastGlitchTime = -999f;

    void Start()
    {
        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGetSettings(out grain);
            volume.profile.TryGetSettings(out vignette);
            volume.profile.TryGetSettings(out chromaticAberration);
        }

        combinedMoveMask = wallLayer | wallByEnemyLayer;
        if (wallLayer == 0) wallLayer = LayerMask.GetMask("Wall");

        SnapToGrid();
        startY = transform.position.y; // ★高さの初期値を記憶
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

        if (Vector3.Distance(transform.position, player.position) < killRange)
        {
            Debug.Log("プレイヤーを捕らえました！");
            Destroy(player.gameObject);
            return;
        }

        HandleFlashlightStun();

        bool canSee = CanSeePlayer();

        if (canSee)
        {
            consecutiveMaxLevelCount = 0;
            stuckTimer = 0f;
        }
        else
        {
            if (useTimeStuckWarp)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckWarpTime)
                {
                    WarpToNearestPoint(); // ★修正済みのワープ
                }
            }
        }

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

    // ★修正：ワープ後に古い場所へ戻らないように状態を完全にリセットする
    void WarpToNearestPoint()
    {
        GameObject[] points = GameObject.FindGameObjectsWithTag(warpPointTag);

        if (points == null || points.Length == 0)
        {
            Debug.LogWarning($"タグ '{warpPointTag}' が付いたオブジェクトが見つかりません。");
            visitLevelMap.Clear();
            stuckTimer = 0f;
            return;
        }

        GameObject nearestPoint = null;
        float minDistance = float.MaxValue;
        foreach (GameObject p in points)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < minDistance) { minDistance = dist; nearestPoint = p; }
        }

        if (nearestPoint != null)
        {
            // 1. 物理位置をワープ
            transform.position = new Vector3(nearestPoint.transform.position.x, nearestPoint.transform.position.y, nearestPoint.transform.position.z);
            startY = transform.position.y; // 新しい高さを基準にする
            SnapToGrid();

            // 2. 思考リセット（ここが重要：古い目的地を消す）
            currentTargetCell = GetGridPosition(transform.position); // 目的地を「現在地」に上書き
            lastSeenCell = new Vector3(9999f, 9999f, 9999f);        // プレイヤーの記憶を消去
            currentState = State.Patrol;                            // パトロールに戻す

            stuckTimer = 0f;
            consecutiveMaxLevelCount = 0;
            visitLevelMap.Clear(); // 訪問記録をリセット

            // 3. その場で新しい進むべき方向を決める
            ChooseNewRandomDirection();

            StartCoroutine(PlayHardGlitch());
            Debug.Log($"<color=cyan>ワープ成功：すべての目的地をリセットしパトロールを再開します。</color>");
        }
    }

    // ワープ直後に進む方向を再決定するヘルパー
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

    void AddVisitLevel(Vector3 pos)
    {
        stuckTimer = 0f;
        int currentLevel = GetVisitLevel(pos);
        if (useLevelLoopWarp && currentLevel >= MAX_VISIT_LEVEL)
        {
            consecutiveMaxLevelCount++;
            if (consecutiveMaxLevelCount >= warpThreshold)
            {
                WarpToNearestPoint();
                return;
            }
        }
        else consecutiveMaxLevelCount = 0;

        if (visitLevelMap.ContainsKey(pos))
        {
            if (visitLevelMap[pos] < MAX_VISIT_LEVEL) visitLevelMap[pos]++;
        }
        else visitLevelMap[pos] = 1;
    }

    // ★修正：浮遊ロジックと目的地チェックを追加
    void MoveTowardsTargetSafe()
    {
        // 安全装置：もし目的地がワープなどで遠すぎる場合はその場でリセット
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z)) > gridSize * 1.5f)
        {
            currentTargetCell = GetGridPosition(transform.position);
            return;
        }

        float speed = (currentState == State.Chase) ? chaseSpeed : moveSpeed;
        float rotSpeed = (currentState == State.Chase) ? rotationSpeed * 1.5f : rotationSpeed;

        if (targetDirection != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(targetDirection), rotSpeed * 100f * Time.deltaTime);

        // 水平方向の移動
        Vector3 horizontalTarget = new Vector3(currentTargetCell.x, transform.position.y, currentTargetCell.z);
        Vector3 nextPos = Vector3.MoveTowards(transform.position, horizontalTarget, speed * Time.deltaTime);

        // ★垂直方向（ふわふわ）の計算
        float floatingY = startY + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        nextPos.y = floatingY;

        if (!Physics.SphereCast(transform.position + Vector3.up, 0.4f, (nextPos - transform.position).normalized, out _, Vector3.Distance(transform.position, nextPos), combinedMoveMask))
        {
            transform.position = nextPos;
        }
        else
        {
            SnapToGrid();
            Vector3 p = transform.position; p.y = floatingY; transform.position = p;
        }
    }

    // --- (以下、既存ロジックの修正・維持) ---

    void SearchLogic()
    {
        if (vignette != null) vignette.enabled.value = false;
        if (chromaticAberration != null) chromaticAberration.enabled.value = false;

        // ★lastSeenCellがリセット（Infinity）されていたらSearchをやめる
        if (lastSeenCell.x > 5000f) { currentState = State.Patrol; return; }

        float dTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z));
        float dLast = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(lastSeenCell.x, 0, lastSeenCell.z));

        if (dLast > 0.1f)
        {
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

    void HandleFlashlightStun()
    {
        if (flashLightHit)
        {
            if (!stunFlg) { saveChaseSpeed = chaseSpeed; saveMoveSpeed = moveSpeed; saveDetectionRange = detectionRange; stunFlg = true; }
            chaseSpeed = 0; moveSpeed = 0; detectionRange = 0;
        }
        else if (stunFlg)
        {
            chaseSpeed = saveChaseSpeed; moveSpeed = saveMoveSpeed; detectionRange = saveDetectionRange; stunFlg = false;
        }
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

    void StartChase() { if (currentState != State.Chase) { currentState = State.Chase; visitLevelMap.Clear(); if (Time.time >= lastGlitchTime + glitchCooldown) { StartCoroutine(PlayHardGlitch()); lastGlitchTime = Time.time; } } }

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
        float d = Vector3.Distance(player.position, transform.position);
        if (vignette != null) vignette.intensity.value = (1f - Mathf.InverseLerp(0f, detectionRange, d)) * 0.4f;

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
        Vector3 bestDir = targetDirection; float minDist = float.MaxValue;
        foreach (Vector3 d in dirs) { if (!Physics.CheckSphere(currentGrid + d * gridSize + Vector3.up, 0.5f, combinedMoveMask)) { float dist = Vector3.Distance(currentGrid + d * gridSize, targetGrid); if (dist < minDist) { minDist = dist; bestDir = d; } } }
        return bestDir;
    }

    int GetVisitLevel(Vector3 pos) => visitLevelMap.ContainsKey(pos) ? visitLevelMap[pos] : 0;
    Vector3 RoundVector(Vector3 v) => new Vector3(Mathf.Round(v.x), 0, Mathf.Round(v.z)).normalized;
    Vector3 GetGridPosition(Vector3 pos) => new Vector3(Mathf.Round(pos.x / gridSize) * gridSize, 0, Mathf.Round(pos.z / gridSize) * gridSize);
    void SnapToGrid() { Vector3 g = GetGridPosition(transform.position); transform.position = new Vector3(g.x, transform.position.y, g.z); }
    Vector3 SortByLevelPriority(List<Vector3> options) { Vector3 cur = GetGridPosition(transform.position); for (int i = 0; i < options.Count; i++) { Vector3 t = options[i]; int r = UnityEngine.Random.Range(i, options.Count); options[i] = options[r]; options[r] = t; } Vector3 best = options[0]; int min = 99; foreach (Vector3 d in options) { int v = GetVisitLevel(cur + d * gridSize); if (v < min) { min = v; best = d; } } return best; }

    void OnDrawGizmos()
    {
        if (showVisitLevels && Application.isPlaying)
        {
            foreach (var entry in visitLevelMap) { int lv = entry.Value; Color c = Color.cyan; if (lv == 2) c = Color.blue; if (lv >= 3) c = Color.magenta; c.a = 0.2f; Gizmos.color = c; Gizmos.DrawCube(entry.Key + Vector3.up * 0.05f, new Vector3(gridSize * 0.9f, 0.1f, gridSize * 0.9f)); }
        }
        Gizmos.color = Color.red; DrawGizmoCircle(transform.position + Vector3.up * 0.2f, killRange);
        Vector3 eyePos = transform.position + Vector3.up;
        Gizmos.color = (currentState == State.Chase) ? Color.red : (currentState == State.Search ? new Color(1f, 0.5f, 0f) : Color.yellow);
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