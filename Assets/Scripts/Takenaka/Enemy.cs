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

    [Header("浮遊設定")]
    public float floatAmplitude = 0.25f;
    public float floatSpeed = 3.0f;
    private float startY;

    [Header("スポーン地点への帰還設定")]
    public bool useReturnToSpawn = true;
    private Vector3 spawnPosition;
    public float returnArrivalDistance = 0.1f;

    [Header("検知設定")]
    public float detectionRange = 10.0f;
    public float killRange = 1.2f;
    public float fovAngle = 90.0f;
    public float searchWaitTime = 2.0f;

    [Header("時計アイテム設定")]
    public float clockEffectRange = 25.0f; // 時計に反応する距離

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

    private enum State { Patrol, Chase, Search, Distracted, Returning }
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

    // =========================================================
    // フラッシュライト関連 (Player.csから操作される)
    // =========================================================
    [NonSerialized] public bool flashLightHit = false;
    private bool stunFlg = false;
    private float saveChaseSpeed = 0f;
    private float saveMoveSpeed = 0f;
    private float saveDetectionRange = 0f;
    private Collider enemyCollider;

    [Header("ワープ発動条件")]
    public bool useLevelLoopWarp = true;
    public int warpThreshold = 5;
    public bool useTimeStuckWarp = true;
    public float stuckWarpTime = 10.0f;
    private int consecutiveMaxLevelCount = 0;
    private float stuckTimer = 0f;
    private Vector3 lastStuckCheckPosition;

    [Header("ノイズ演出設定")]
    [SerializeField] private float glitchDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float stretchIntensity = 2.0f;
    [SerializeField] private float glitchCooldown = 5.0f;
    private float lastGlitchTime = -999f;

    void Start()
    {
        enemyCollider = GetComponent<Collider>();
        if (volume != null)
        {
            volume.profile.TryGetSettings(out grain);
            volume.profile.TryGetSettings(out vignette);
            volume.profile.TryGetSettings(out chromaticAberration);
        }
        combinedMoveMask = wallLayer | wallByEnemyLayer;
        SnapToGrid();
        spawnPosition = transform.position;
        startY = transform.position.y;
        currentTargetCell = GetGridPosition(transform.position);
        targetDirection = transform.forward;
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null) player = playerObj.transform;

        saveChaseSpeed = chaseSpeed;
        saveMoveSpeed = moveSpeed;
        saveDetectionRange = detectionRange;
        lastStuckCheckPosition = transform.position;
    }

    void Update()
    {
        if (player == null) return;

        // フラッシュライトのスタン処理
        HandleFlashlightStun();
        if (stunFlg) return;

        // キル判定
        if (enemyCollider != null && enemyCollider.enabled)
        {
            Vector3 localPlayerPos = transform.InverseTransformPoint(player.position);
            if (Mathf.Abs(localPlayerPos.x) < killRange && Mathf.Abs(localPlayerPos.z) < killRange)
            {
                SceneManager.LoadScene("GameOverScene");
                return;
            }
        }

        // ドア破壊中は移動停止
        if (CheckAndBreakDoor()) return;

        // プレイヤー視認チェック
        bool canSee = CanSeePlayer();

        // =========================================================
        // 時計アイテムの状態判定 (最優先)
        // =========================================================
        if (Player.Instance != null && Player.Instance.clockFlg)
        {
            float distToClock = Vector3.Distance(transform.position, Player.Instance.GetClockPos());
            // 時計が有効かつ、効果範囲内にいる場合、帰還中以外なら時計を優先する
            if (distToClock <= clockEffectRange && currentState != State.Returning)
            {
                if (currentState != State.Distracted)
                {
                    currentState = State.Distracted;
                }
            }
        }

        // =========================================================
        // 状態遷移スイッチ
        // =========================================================
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
                // 時計に気を取られている間は canSee を無視して時計へ向かう
                DistractedLogic();
                break;

            case State.Returning:
                ReturnToSpawnLogic();
                break;
        }

        HandleStuckWarp();
    }

    // =========================================================
    // 時計誘導ロジック
    // =========================================================
    void DistractedLogic()
    {
        // プレイヤーが時計を止めた、もしくは効果時間が切れた場合
        if (Player.Instance == null || !Player.Instance.clockFlg)
        {
            currentState = State.Search;
            return;
        }

        Vector3 clockPos = Player.Instance.GetClockPos();
        Vector3 clockGrid = GetGridPosition(clockPos);

        // 次の目的地（グリッド）に到達したか判定
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z)) < 0.1f)
        {
            Vector3 currentGrid = GetGridPosition(transform.position);

            // 時計の場所に到着したか
            if (Vector3.Distance(currentGrid, clockGrid) < 0.1f)
            {
                // 到着したらその場で索敵状態へ（時計のフラグは他者のために残す）
                currentState = State.Search;
                return;
            }

            // 時計の方へ向かう最適な方向を計算
            Vector3 nextDir = GetBestDirectionTowards(currentGrid, clockGrid);
            targetDirection = (nextDir != Vector3.zero) ? nextDir : transform.forward;
            currentTargetCell = currentGrid + targetDirection * gridSize;
        }

        MoveTowardsTargetSafe();
    }

    // =========================================================
    // 各種ロジック・ユーティリティ
    // =========================================================

    private void HandleFlashlightStun()
    {
        if (flashLightHit)
        {
            if (!stunFlg)
            {
                saveChaseSpeed = chaseSpeed;
                saveMoveSpeed = moveSpeed;
                saveDetectionRange = detectionRange;
                stunFlg = true;
                if (enemyCollider != null) enemyCollider.enabled = false;
            }
            chaseSpeed = 0; moveSpeed = 0; detectionRange = 0;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }
        else if (!flashLightHit && stunFlg)
        {
            chaseSpeed = saveChaseSpeed; moveSpeed = saveMoveSpeed; detectionRange = saveDetectionRange;
            stunFlg = false;
            if (enemyCollider != null) enemyCollider.enabled = true;
        }
    }

    void MoveTowardsTargetSafe()
    {
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z)) > gridSize * 1.5f)
        { currentTargetCell = GetGridPosition(transform.position); return; }

        float speed = (currentState == State.Chase) ? chaseSpeed : moveSpeed;
        float rotSpeed = (currentState == State.Chase) ? rotationSpeed * 1.5f : rotationSpeed;

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }

        Vector3 horizontalTarget = new Vector3(currentTargetCell.x, transform.position.y, currentTargetCell.z);
        Vector3 nextPos = Vector3.MoveTowards(transform.position, horizontalTarget, speed * Time.deltaTime);
        nextPos.y = startY + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;

        Vector3 moveDir = (nextPos - transform.position).normalized;
        float moveDist = Vector3.Distance(transform.position, nextPos);

        if (moveDist > 0.001f && !Physics.SphereCast(transform.position + Vector3.up, 0.3f, moveDir, out _, moveDist, combinedMoveMask))
            transform.position = nextPos;
        else if (Vector3.Distance(transform.position, horizontalTarget) < 0.05f)
            transform.position = new Vector3(horizontalTarget.x, nextPos.y, horizontalTarget.z);
    }

    void PatrolLogic()
    {
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z)) < 0.1f)
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
        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z)) < 0.1f)
        {
            Vector3 p = GetGridPosition(transform.position);
            AddVisitLevel(p);
            targetDirection = GetBestDirectionTowards(p, GetGridPosition(player.position));
            if (targetDirection != Vector3.zero) currentTargetCell = p + targetDirection * gridSize;
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
            if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(currentTargetCell.x, 0, currentTargetCell.z)) < 0.1f)
            {
                Vector3 p = GetGridPosition(transform.position);
                AddVisitLevel(p);
                targetDirection = GetBestDirectionTowards(p, lastSeenCell);
                if (targetDirection != Vector3.zero) currentTargetCell = p + targetDirection * gridSize;
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

    bool CanSeePlayer()
    {
        if (Vector3.Distance(transform.position, player.position) > detectionRange) return false;
        if (Vector3.Angle(transform.forward, (player.position - transform.position).normalized) < fovAngle * 0.5f)
        {
            if (!Physics.Linecast(transform.position + Vector3.up, player.position + Vector3.up, wallLayer))
            {
                lastSeenCell = GetGridPosition(player.position);
                return true;
            }
        }
        return false;
    }

    // グリッド系ユーティリティ
    Vector3 GetGridPosition(Vector3 pos) => new Vector3(Mathf.Round(pos.x / gridSize) * gridSize, 0, Mathf.Round(pos.z / gridSize) * gridSize);
    void SnapToGrid() { Vector3 g = GetGridPosition(transform.position); transform.position = new Vector3(g.x, transform.position.y, g.z); }
    Vector3 RoundVector(Vector3 v) => new Vector3(Mathf.Round(v.x), 0, Mathf.Round(v.z)).normalized;

    Vector3 GetBestDirectionTowards(Vector3 currentGrid, Vector3 targetGrid)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        Vector3 bestDir = Vector3.zero;
        float minDist = float.MaxValue;
        bool found = false;
        foreach (Vector3 d in dirs)
        {
            Vector3 checkPos = currentGrid + d * gridSize;
            if (!Physics.CheckSphere(checkPos + Vector3.up, 0.4f, combinedMoveMask))
            {
                float dist = Vector3.Distance(checkPos, targetGrid);
                if (dist < minDist) { minDist = dist; bestDir = d; found = true; }
            }
        }
        return found ? bestDir : Vector3.zero;
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
        if (canRgt) sides.Add(rgt);
        if (canLft) sides.Add(lft);
        if (!canFwd) { targetDirection = sides.Count > 0 ? SortByLevelPriority(sides) : bck; straightStepCount = 0; }
        else if (sides.Count > 0 && straightStepCount >= minStraightSteps)
        {
            Vector3 best = SortByLevelPriority(sides);
            if (GetVisitLevel(currentPos + best * gridSize) < GetVisitLevel(currentPos + fwd * gridSize) || UnityEngine.Random.value < turnProbability)
            { targetDirection = best; straightStepCount = 0; }
            else straightStepCount++;
        }
        else straightStepCount++;
        currentTargetCell = currentPos + targetDirection * gridSize;
    }

    void AddVisitLevel(Vector3 pos)
    {
        pos = GetGridPosition(pos);
        if (!visitLevelMap.ContainsKey(pos)) { visitLevelMap[pos] = 1; consecutiveMaxLevelCount = 0; }
        else
        {
            visitLevelMap[pos]++;
            if (visitLevelMap[pos] >= MAX_VISIT_LEVEL) { visitLevelMap[pos] = MAX_VISIT_LEVEL; consecutiveMaxLevelCount++; }
            else consecutiveMaxLevelCount = 0;
        }
        if (useLevelLoopWarp && consecutiveMaxLevelCount >= warpThreshold) WarpToNearestTaggedPoint();
    }

    int GetVisitLevel(Vector3 pos) => visitLevelMap.ContainsKey(pos) ? visitLevelMap[pos] : 0;

    Vector3 SortByLevelPriority(List<Vector3> options)
    {
        Vector3 cur = GetGridPosition(transform.position);
        for (int i = 0; i < options.Count; i++) { int r = UnityEngine.Random.Range(i, options.Count); Vector3 temp = options[i]; options[i] = options[r]; options[r] = temp; }
        Vector3 best = options[0]; int min = 99;
        foreach (Vector3 d in options) { int v = GetVisitLevel(cur + d * gridSize); if (v < min) { min = v; best = d; } }
        return best;
    }

    void StartChase() { if (currentState != State.Chase) { currentState = State.Chase; visitLevelMap.Clear(); StartCoroutine(PlayHardGlitch()); } }

    bool CheckAndBreakDoor()
    {
        Vector3 checkCenter = transform.position + transform.right * doorCheckOffset.x + transform.up * doorCheckOffset.y + transform.forward * doorCheckOffset.z;
        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, doorBreakRadius);
        bool foundDoor = false;
        foreach (var hit in hitColliders) if (hit.CompareTag("Door") || hit.CompareTag("wDoor")) { foundDoor = true; break; }
        if (foundDoor)
        {
            doorBreakTimer += Time.deltaTime;
            if (doorBreakTimer >= doorBreakTime)
            {
                foreach (var hit in hitColliders) if (hit.CompareTag("Door") || hit.CompareTag("wDoor")) Destroy(hit.gameObject);
                doorBreakTimer = 0f;
            }
            return true;
        }
        doorBreakTimer = 0f;
        return false;
    }

    void WarpToNearestTaggedPoint()
    {
        GameObject[] points = GameObject.FindGameObjectsWithTag(warpPointTag);
        if (points == null || points.Length == 0) return;
        GameObject nearest = null; float minD = float.MaxValue;
        foreach (GameObject p in points) { float d = Vector3.Distance(transform.position, p.transform.position); if (d < minD) { minD = d; nearest = p; } }
        if (nearest != null)
        {
            transform.position = nearest.transform.position;
            startY = transform.position.y; SnapToGrid(); currentTargetCell = GetGridPosition(transform.position);
            currentState = State.Patrol; lastSeenCell = new Vector3(9999f, 9999f, 9999f);
            searchTimer = 0f; stuckTimer = 0f; consecutiveMaxLevelCount = 0; visitLevelMap.Clear(); ChooseNewRandomDirection();
        }
    }

    void ChooseNewRandomDirection()
    {
        Vector3 currentPos = GetGridPosition(transform.position);
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        List<Vector3> valid = new List<Vector3>();
        foreach (Vector3 d in dirs) if (!Physics.CheckSphere(currentPos + d * gridSize + Vector3.up, 0.5f, combinedMoveMask)) valid.Add(d);
        targetDirection = valid.Count > 0 ? valid[UnityEngine.Random.Range(0, valid.Count)] : transform.forward;
        currentTargetCell = currentPos + targetDirection * gridSize;
    }

    private void HandleStuckWarp()
    {
        if (useTimeStuckWarp && currentState != State.Returning)
        {
            Vector3 currentHorizontalPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 lastHorizontalPos = new Vector3(lastStuckCheckPosition.x, 0f, lastStuckCheckPosition.z);
            if (Vector3.Distance(currentHorizontalPos, lastHorizontalPos) < 0.01f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckWarpTime) { WarpToNearestTaggedPoint(); stuckTimer = 0f; }
            }
            else { stuckTimer = 0f; lastStuckCheckPosition = transform.position; }
        }
    }

    void ReturnToSpawnLogic()
    {
        float distance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(spawnPosition.x, 0f, spawnPosition.z));
        if (distance <= returnArrivalDistance) { TeleportToSpawn(); return; }
        if (Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(currentTargetCell.x, 0f, currentTargetCell.z)) < 0.1f)
        {
            Vector3 currentGrid = GetGridPosition(transform.position);
            targetDirection = GetBestDirectionTowards(currentGrid, GetGridPosition(spawnPosition));
            currentTargetCell = currentGrid + targetDirection * gridSize;
        }
        MoveTowardsTargetSafe();
    }

    public void TeleportToSpawn()
    {
        transform.position = spawnPosition; currentTargetCell = GetGridPosition(spawnPosition);
        currentState = State.Patrol; targetDirection = transform.forward; straightStepCount = 0;
        searchTimer = 0f; stuckTimer = 0f; doorBreakTimer = 0f;
        lastSeenCell = new Vector3(9999f, 9999f, 9999f); visitLevelMap.Clear();
        startY = spawnPosition.y; lastStuckCheckPosition = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (enemyCollider != null && enemyCollider.enabled && collision.gameObject.CompareTag(playerTag))
            SceneManager.LoadScene("GameOverScene");
    }

    IEnumerator PlayHardGlitch()
    {
        Camera cam = Camera.main; if (cam == null) yield break;
        float oAspect = cam.aspect; float oFOV = cam.fieldOfView; Vector3 oPos = cam.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < glitchDuration)
        {
            cam.transform.localPosition = oPos + UnityEngine.Random.insideUnitSphere * shakeIntensity;
            cam.aspect = oAspect * UnityEngine.Random.Range(1f / stretchIntensity, stretchIntensity);
            cam.fieldOfView = oFOV + UnityEngine.Random.Range(-15f, 15f);
            if (grain != null) grain.enabled.value = true;
            elapsed += Time.unscaledDeltaTime; yield return null;
        }
        cam.ResetAspect(); if (grain != null) grain.enabled.value = false; cam.fieldOfView = oFOV; cam.transform.localPosition = oPos;
    }

    void OnDrawGizmos()
    {
        if (showVisitLevels && Application.isPlaying)
        {
            foreach (var entry in visitLevelMap)
            {
                Gizmos.color = entry.Value == 1 ? Color.cyan : (entry.Value == 2 ? Color.blue : Color.magenta);
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.2f);
                Gizmos.DrawCube(entry.Key + Vector3.up * 0.05f, new Vector3(gridSize * 0.9f, 0.1f, gridSize * 0.9f));
            }
        }
        Gizmos.color = Color.red;
        Matrix4x4 oldM = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 0.2f, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(killRange * 2f, 0.1f, killRange * 2f));
        Gizmos.matrix = oldM;

        // 時計の反応範囲を可視化
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, clockEffectRange);

        Vector3 eye = transform.position + Vector3.up;
        Gizmos.color = currentState == State.Chase ? Color.red : (currentState == State.Search ? Color.yellow : Color.white);
        Gizmos.DrawRay(eye, Quaternion.Euler(0, -fovAngle * 0.5f, 0) * transform.forward * detectionRange);
        Gizmos.DrawRay(eye, Quaternion.Euler(0, fovAngle * 0.5f, 0) * transform.forward * detectionRange);
    }
}