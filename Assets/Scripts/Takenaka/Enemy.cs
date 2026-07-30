using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

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

    [Header("ドア破壊設定")]
    public float doorBreakTime = 2.0f;
    public float doorBreakRadius = 1.0f;
    public Vector3 doorCheckOffset = new Vector3(0, 1.0f, 0.8f);
    private float doorBreakTimer = 0f;

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

    [Header("スタック対策設定")]
    [Tooltip("同じ場所（レベル3）をループした時にワープするか")]
    public bool useLevelLoopWarp = true;     // 追加：インスペクターでOFFにできる
    public int warpThreshold = 5;

    [Tooltip("一定時間移動がない時にワープするか")]
    public bool useTimeStuckWarp = true;    // 追加：インスペクターでOFFにできる
    public float stuckWarpTime = 10.0f;

    private int consecutiveMaxLevelCount = 0;
    private float stuckTimer = 0f;

    [Header("ワープエリア設定")]
    public LayerMask warpAreaLayer;

    [Header("ノイズ演出設定")]
    [SerializeField] private float glitchDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float stretchIntensity = 2.0f;
    [SerializeField] private float glitchCooldown = 5.0f;
    private float lastGlitchTime = -999f;

    void Start()
    {
        volume.profile.TryGetSettings(out grain);
        volume.profile.TryGetSettings(out vignette);
        volume.profile.TryGetSettings(out chromaticAberration);

        combinedMoveMask = wallLayer | wallByEnemyLayer;
        if (wallLayer == 0) wallLayer = LayerMask.GetMask("Wall");

        SnapToGrid();
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

        // 1. プレイヤーとの死亡判定
        if (Vector3.Distance(transform.position, player.position) < killRange)
        {
            Debug.Log("プレイヤーを捕らえました！");
            Destroy(player.gameObject);
            return;
        }

        HandleFlashlightStun();
        if (CheckAndBreakDoor()) return;

        bool canSee = CanSeePlayer();

        if (canSee)
        {
            consecutiveMaxLevelCount = 0;
            stuckTimer = 0f;
        }
        else
        {
            // ★時間経過によるスタック判定（有効な場合のみ）
            if (useTimeStuckWarp)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckWarpTime)
                {
                    Debug.Log("<color=red>一定時間レベル更新がないためワープします。</color>");
                    WarpToLowPriorityInArea();
                    stuckTimer = 0f;
                }
            }
        }

        // --- 以下、Stateに応じたロジック ---
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

    // レベルを更新する際に呼ばれるメソッド
    void AddVisitLevel(Vector3 pos)
    {
        stuckTimer = 0f; // マスが動いたのでタイマーは常にリセット

        int currentLevel = GetVisitLevel(pos);

        // ★レベルループによるワープ判定（有効な場合のみ）
        if (useLevelLoopWarp && currentLevel >= MAX_VISIT_LEVEL)
        {
            consecutiveMaxLevelCount++;
            if (consecutiveMaxLevelCount >= warpThreshold)
            {
                Debug.Log("<color=yellow>ループ検知によりワープします。</color>");
                WarpToLowPriorityInArea();
                consecutiveMaxLevelCount = 0;
                return;
            }
        }
        else
        {
            consecutiveMaxLevelCount = 0;
        }

        if (visitLevelMap.ContainsKey(pos))
        {
            if (visitLevelMap[pos] < MAX_VISIT_LEVEL) visitLevelMap[pos]++;
        }
        else visitLevelMap[pos] = 1;
    }

    // --- ワープ処理 ---
    WarpArea GetCurrentWarpArea()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.5f, warpAreaLayer);
        foreach (var col in colliders)
        {
            WarpArea area = col.GetComponent<WarpArea>();
            if (area == null) area = col.GetComponentInParent<WarpArea>();
            if (area != null) return area;
        }
        return null;
    }

    void WarpToLowPriorityInArea()
    {
        WarpArea currentArea = GetCurrentWarpArea();
        if (currentArea == null || currentArea.areaCollider == null)
        {
            Debug.LogWarning("WarpAreaが見つかりません。記憶をリセットします。");
            visitLevelMap.Clear();
            return;
        }

        List<Vector3> candidates = new List<Vector3>();
        Bounds bounds = currentArea.areaCollider.bounds;

        for (float x = bounds.min.x; x <= bounds.max.x; x += gridSize)
        {
            for (float z = bounds.min.z; z <= bounds.max.z; z += gridSize)
            {
                Vector3 checkPos = GetGridPosition(new Vector3(x, 0, z));
                if (currentArea.areaCollider.ClosestPoint(checkPos) == checkPos)
                {
                    if (!visitLevelMap.ContainsKey(checkPos)) // 未踏地点(Level 0)を探す
                    {
                        if (!Physics.CheckSphere(checkPos + Vector3.up, 0.5f, combinedMoveMask))
                        {
                            candidates.Add(checkPos);
                        }
                    }
                }
            }
        }

        if (candidates.Count > 0)
        {
            Vector3 warpTarget = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            transform.position = new Vector3(warpTarget.x, transform.position.y, warpTarget.z);
            currentTargetCell = warpTarget;
            SnapToGrid();

            // ワープ後は全てのカウントをリセット
            consecutiveMaxLevelCount = 0;
            stuckTimer = 0f;

            StartCoroutine(PlayHardGlitch());
            Debug.Log($"<color=cyan>エリア '{currentArea.name}' 内へワープ完了。</color>");
        }
        else
        {
            ClearMemoryInBounds(bounds);
            Debug.Log("未踏地点がないため、エリア内の記憶を消去しました。");
        }
    }

    void ClearMemoryInBounds(Bounds bounds)
    {
        List<Vector3> keysToRemove = new List<Vector3>();
        foreach (var pos in visitLevelMap.Keys)
        {
            if (bounds.Contains(pos)) keysToRemove.Add(pos);
        }
        foreach (var key in keysToRemove) visitLevelMap.Remove(key);
    }

    // --- (以下、ドア破壊、移動、Gizmosなどの既存コード) ---
    bool CheckAndBreakDoor()
    {
        Vector3 checkCenter = transform.position + transform.right * doorCheckOffset.x + transform.up * doorCheckOffset.y + transform.forward * doorCheckOffset.z;
        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, doorBreakRadius);
        bool foundDoor = false;
        List<GameObject> doorsInRange = new List<GameObject>();
        foreach (var hit in hitColliders) { if (hit.CompareTag("Door") || hit.CompareTag("wDoor")) { foundDoor = true; doorsInRange.Add(hit.gameObject); } }
        if (foundDoor) { doorBreakTimer += Time.deltaTime; if (doorBreakTimer >= doorBreakTime) { foreach (GameObject d in doorsInRange) if (d != null) Destroy(d); doorBreakTimer = 0f; } return true; }
        doorBreakTimer = 0f; return false;
    }

    void HandleFlashlightStun()
    {
        if (flashLightHit) { if (!stunFlg) { saveChaseSpeed = chaseSpeed; saveMoveSpeed = moveSpeed; saveDetectionRange = detectionRange; stunFlg = true; } chaseSpeed = 0; moveSpeed = 0; detectionRange = 0; }
        else if (!flashLightHit && stunFlg) { chaseSpeed = saveChaseSpeed; moveSpeed = saveMoveSpeed; detectionRange = saveDetectionRange; stunFlg = false; }
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
    void PatrolLogic() { float d = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell); if (d < 0.05f) { Vector3 p = GetGridPosition(transform.position); AddVisitLevel(p); UpdateNextPatrolTarget(p); } MoveTowardsTargetSafe(); }
    void ChaseLogic() { vignette.enabled.value = true; chromaticAberration.enabled.value = true; float d = Vector3.Distance(player.position, transform.position); vignette.intensity.value = (1f - Mathf.InverseLerp(0f, detectionRange, d)) * 0.4f; if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell) < 0.05f) { Vector3 p = GetGridPosition(transform.position); AddVisitLevel(p); targetDirection = GetBestDirectionTowards(p, GetGridPosition(player.position)); currentTargetCell = p + targetDirection * gridSize; } MoveTowardsTargetSafe(); }
    void SearchLogic() { vignette.enabled.value = false; chromaticAberration.enabled.value = false; float dTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell); float dLast = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), lastSeenCell); if (dLast > 0.1f) { if (dTarget < 0.05f) { Vector3 p = GetGridPosition(transform.position); AddVisitLevel(p); targetDirection = GetBestDirectionTowards(p, lastSeenCell); currentTargetCell = p + targetDirection * gridSize; } MoveTowardsTargetSafe(); searchTimer = 0f; } else { searchTimer += Time.deltaTime; if (searchTimer >= searchWaitTime) currentState = State.Patrol; } }

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

    void MoveTowardsTargetSafe()
    {
        float speed = (currentState == State.Chase) ? chaseSpeed : moveSpeed;
        float rotSpeed = (currentState == State.Chase) ? rotationSpeed * 1.5f : rotationSpeed;
        if (targetDirection != Vector3.zero) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(targetDirection), rotSpeed * 100f * Time.deltaTime);
        Vector3 next = Vector3.MoveTowards(transform.position, new Vector3(currentTargetCell.x, transform.position.y, currentTargetCell.z), speed * Time.deltaTime);
        if (!Physics.SphereCast(transform.position + Vector3.up, 0.4f, (next - transform.position).normalized, out _, Vector3.Distance(transform.position, next), combinedMoveMask)) transform.position = next;
        else SnapToGrid();
    }

    int GetVisitLevel(Vector3 pos) => visitLevelMap.ContainsKey(pos) ? visitLevelMap[pos] : 0;
    private void OnCollisionEnter(Collision collision) { if (collision.gameObject.CompareTag(playerTag)) Destroy(collision.gameObject); }
    Vector3 RoundVector(Vector3 v) => new Vector3(Mathf.Round(v.x), 0, Mathf.Round(v.z)).normalized;
    Vector3 GetGridPosition(Vector3 pos) => new Vector3(Mathf.Round(pos.x / gridSize) * gridSize, 0, Mathf.Round(pos.z / gridSize) * gridSize);
    void SnapToGrid() { Vector3 g = GetGridPosition(transform.position); transform.position = new Vector3(g.x, transform.position.y, g.z); }
    Vector3 SortByLevelPriority(List<Vector3> options) { Vector3 cur = GetGridPosition(transform.position); for (int i = 0; i < options.Count; i++) { Vector3 t = options[i]; int r = UnityEngine.Random.Range(i, options.Count); options[i] = options[r]; options[r] = t; } Vector3 best = options[0]; int min = 99; foreach (Vector3 d in options) { int v = GetVisitLevel(cur + d * gridSize); if (v < min) { min = v; best = d; } } return best; }

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
        Gizmos.color = (currentState == State.Chase) ? Color.red : (currentState == State.Search ? new Color(1f, 0.5f, 0f) : Color.yellow);
        Vector3 left = Quaternion.Euler(0, -fovAngle * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, fovAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(eyePos, left * detectionRange); Gizmos.DrawRay(eyePos, right * detectionRange);
        Vector3 checkCenter = transform.position + transform.right * doorCheckOffset.x + transform.up * doorCheckOffset.y + transform.forward * doorCheckOffset.z;
        Gizmos.color = (doorBreakTimer > 0) ? Color.red : Color.green; Gizmos.DrawWireSphere(checkCenter, doorBreakRadius);
        if (doorBreakTimer > 0) { Gizmos.color = new Color(1, 0, 0, 0.3f); Gizmos.DrawSphere(checkCenter, doorBreakRadius * (doorBreakTimer / doorBreakTime)); }
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
            grain.enabled.value = true; elapsed += Time.unscaledDeltaTime; yield return null;
        }
        cam.ResetAspect(); grain.enabled.value = false; cam.fieldOfView = originalFOV; cam.transform.localPosition = originalPos;
    }
}