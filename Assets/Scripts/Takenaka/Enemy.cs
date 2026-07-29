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
    public float doorBreakTime = 2.0f;     // 破壊にかかる秒数
    public float doorBreakRadius = 1.0f;   // 破壊の範囲（半径） ★
    public Vector3 doorCheckOffset = new Vector3(0, 1.0f, 0.8f); // 敵から見た判定の中心位置 ★
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
    public int warpThreshold = 5;          // 連続してMAX地点を通った時の閾値
    public int minWarpDistance = 3;        // ワープ先の最小距離（マス数）
    public int maxWarpDistance = 10;       // ワープ先の最大距離（マス数）
    private int consecutiveMaxLevelCount = 0; // 連続カウント用

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

        // 2. フラッシュライトスタン
        HandleFlashlightStun();

        // 3. ドア破壊（ここがtrueなら以降の移動は行わない）
        if (CheckAndBreakDoor()) return;

        bool canSee = CanSeePlayer();

        // ★ 修正箇所：変数名を連続カウント用に変更
        // プレイヤーを発見した瞬間、または追跡中はスタック判定を行わないようにリセットする
        if (canSee)
        {
            consecutiveMaxLevelCount = 0;
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

    // --- ドア破壊用の新規メソッド ---
    bool CheckAndBreakDoor()
    {
        // 判定の中心点を計算（敵の向きに合わせてオフセットを移動させる）
        Vector3 checkCenter = transform.position +
                              transform.right * doorCheckOffset.x +
                              transform.up * doorCheckOffset.y +
                              transform.forward * doorCheckOffset.z;

        // 指定した半径内のコライダーをすべて取得
        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, doorBreakRadius);

        bool foundDoor = false;
        List<GameObject> doorsInRange = new List<GameObject>();

        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Door") || hit.CompareTag("wDoor"))
            {
                foundDoor = true;
                doorsInRange.Add(hit.gameObject);
            }
        }

        if (foundDoor)
        {
            // ドアがある間はタイマーを進める
            doorBreakTimer += Time.deltaTime;

            if (doorBreakTimer >= doorBreakTime)
            {
                // 範囲内のドアをすべて破壊
                foreach (GameObject door in doorsInRange)
                {
                    if (door != null) Destroy(door);
                }
                doorBreakTimer = 0f;
                Debug.Log("範囲内のドアを破壊しました。");
            }
            return true; // 移動を止める
        }

        // 範囲内にドアがないならリセット
        doorBreakTimer = 0f;
        return false;
    }

    // スタン処理をUpdateから分離（見やすくするため）
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
            }
            chaseSpeed = 0;
            moveSpeed = 0;
            detectionRange = 0;
        }
        else if (!flashLightHit && stunFlg)
        {
            chaseSpeed = saveChaseSpeed;
            moveSpeed = saveMoveSpeed;
            detectionRange = saveDetectionRange;
            stunFlg = false;
        }
    }

    // --- (以下、元のメソッド群を継続) ---

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

    void StartChase()
    {
        if (currentState != State.Chase)
        {
            currentState = State.Chase;
            visitLevelMap.Clear();
            Debug.Log("プレイヤー発見！レベルをリセットしました");

            if (Time.time >= lastGlitchTime + glitchCooldown)
            {
                StartCoroutine(PlayHardGlitch());
                lastGlitchTime = Time.time;
            }
        }
    }

    void PatrolLogic()
    {
        float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell);
        if (distToTarget < 0.05f)
        {
            Vector3 currentPos = GetGridPosition(transform.position);
            AddVisitLevel(currentPos);
            UpdateNextPatrolTarget(currentPos);
        }
        MoveTowardsTargetSafe();
    }

    void ChaseLogic()
    {
        vignette.enabled.value = true;
        chromaticAberration.enabled.value = true;

        float distance = Vector3.Distance(player.position, this.transform.position);
        float t = 1f - Mathf.InverseLerp(0f, detectionRange, distance);
        float value = t * 0.4f;
        vignette.intensity.value = value;

        float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell);
        if (distToTarget < 0.05f)
        {
            Vector3 currentPos = GetGridPosition(transform.position);
            AddVisitLevel(currentPos);

            Vector3 playerGridPos = GetGridPosition(player.position);
            targetDirection = GetBestDirectionTowards(currentPos, playerGridPos);
            currentTargetCell = currentPos + targetDirection * gridSize;
        }
        MoveTowardsTargetSafe();
    }

    void SearchLogic()
    {
        vignette.enabled.value = false;
        chromaticAberration.enabled.value = false;

        float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), currentTargetCell);
        float distToLastSeen = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), lastSeenCell);

        if (distToLastSeen > 0.1f)
        {
            if (distToTarget < 0.05f)
            {
                Vector3 currentPos = GetGridPosition(transform.position);
                AddVisitLevel(currentPos);
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
            if (GetVisitLevel(currentPos + bestSide * gridSize) < GetVisitLevel(currentPos + fwd * gridSize) || UnityEngine.Random.value < turnProbability)
            {
                targetDirection = bestSide;
                straightStepCount = 0;
            }
            else straightStepCount++;
        }
        else straightStepCount++;

        currentTargetCell = currentPos + targetDirection * gridSize;
    }

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

    void MoveTowardsTargetSafe()
    {
        float currentSpeed = (currentState == State.Chase) ? chaseSpeed : moveSpeed;
        float currentRotationSpeed = (currentState == State.Chase) ? rotationSpeed * 1.5f : rotationSpeed;

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, currentRotationSpeed * 100f * Time.deltaTime);
        }

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

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            Destroy(collision.gameObject);
        }
    }

    void AddVisitLevel(Vector3 pos)
    {
        int currentLevel = GetVisitLevel(pos);

        // ★連続性チェック：今踏んだ場所がレベルMAX(3)かどうか
        if (currentLevel >= MAX_VISIT_LEVEL)
        {
            consecutiveMaxLevelCount++;
            Debug.Log($"<color=yellow>連続スタック警戒: {consecutiveMaxLevelCount}/{warpThreshold}</color>");

            if (consecutiveMaxLevelCount >= warpThreshold)
            {
                WarpToRandomNearbyLevel0(); // ランダムワープ実行
                consecutiveMaxLevelCount = 0;
                return;
            }
        }
        else
        {
            // ★3以外のレベル(0,1,2)を通った瞬間、カウントをリセット
            if (consecutiveMaxLevelCount > 0)
            {
                Debug.Log("<color=white>スタックカウントをリセットしました（未踏エリアに到達）</color>");
            }
            consecutiveMaxLevelCount = 0;
        }

        // 通常のレベル加算処理
        if (visitLevelMap.ContainsKey(pos))
        {
            if (visitLevelMap[pos] < MAX_VISIT_LEVEL) visitLevelMap[pos]++;
        }
        else
        {
            visitLevelMap[pos] = 1;
        }
    }

    void WarpToRandomNearbyLevel0()
    {
        Vector3 currentPos = GetGridPosition(transform.position);
        List<Vector3> candidates = new List<Vector3>();

        // 自分の周りの一定範囲（minWarpDistance ～ maxWarpDistance）を探索
        for (int x = -maxWarpDistance; x <= maxWarpDistance; x++)
        {
            for (int z = -maxWarpDistance; z <= maxWarpDistance; z++)
            {
                int dist = Mathf.Abs(x) + Mathf.Abs(z); // マンハッタン距離

                // 近すぎず、遠すぎない範囲内かチェック
                if (dist < minWarpDistance || dist > maxWarpDistance) continue;

                Vector3 checkPos = currentPos + new Vector3(x, 0, z) * gridSize;

                // レベル0（まだ通っていない）かつ、壁がない場所を候補に入れる
                if (!visitLevelMap.ContainsKey(checkPos))
                {
                    if (!Physics.CheckSphere(checkPos + Vector3.up, 0.5f, combinedMoveMask))
                    {
                        candidates.Add(checkPos);
                    }
                }
            }
        }

        if (candidates.Count > 0)
        {
            // ★候補の中からランダムに一つ選ぶ
            Vector3 warpTarget = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            transform.position = new Vector3(warpTarget.x, transform.position.y, warpTarget.z);
            currentTargetCell = warpTarget;
            SnapToGrid();

            StartCoroutine(PlayHardGlitch());
            Debug.Log($"<color=cyan>付近の未踏地点（全{candidates.Count}候補）からランダムにワープしました。</color>");
        }
        else
        {
            // 周辺にレベル0がない場合はマップ記憶をリセット
            visitLevelMap.Clear();
            Debug.Log("周辺に未踏地点がないため、訪問記録をリセットしました。");
        }
    }

    int GetVisitLevel(Vector3 pos) => visitLevelMap.ContainsKey(pos) ? visitLevelMap[pos] : 0;

    void OnDrawGizmos()
    {
        // 1. 既存の巡回経路などのデバッグ表示
        if (showVisitLevels && Application.isPlaying)
        {
            foreach (var entry in visitLevelMap)
            {
                int level = entry.Value;
                Color c = Color.cyan;
                if (level == 2) c = Color.blue;
                if (level >= 3) c = Color.magenta;
                c.a = 0.2f;
                Gizmos.color = c;
                Gizmos.DrawCube(entry.Key + Vector3.up * 0.05f, new Vector3(gridSize * 0.9f, 0.1f, gridSize * 0.9f));
            }
        }

        // 2. プレイヤー殺傷範囲
        Gizmos.color = Color.red;
        DrawGizmoCircle(transform.position + Vector3.up * 0.2f, killRange);

        // 3. 視界（FOV）の表示
        Vector3 eyePos = transform.position + Vector3.up;
        Color fovColor = Color.yellow;
        if (currentState == State.Chase) fovColor = Color.red;
        else if (currentState == State.Search) fovColor = new Color(1f, 0.5f, 0f);

        Gizmos.color = fovColor;
        Vector3 leftBoundary = Quaternion.Euler(0, -fovAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, fovAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(eyePos, leftBoundary * detectionRange);
        Gizmos.DrawRay(eyePos, rightBoundary * detectionRange);

        // (中略：FOVの扇形表示ロジックはそのまま)

        // 4. ドア破壊範囲のデバッグ表示 ★ここを整理しました★
        Vector3 checkCenter;
        if (Application.isPlaying)
        {
            checkCenter = transform.position +
                          transform.right * doorCheckOffset.x +
                          transform.up * doorCheckOffset.y +
                          transform.forward * doorCheckOffset.z;
        }
        else
        {
            // エディタ停止中も位置を確認できるように
            checkCenter = transform.position + transform.forward * doorCheckOffset.z + transform.up * doorCheckOffset.y;
        }

        // 破壊中なら赤、待機中なら緑
        Gizmos.color = (doorBreakTimer > 0) ? Color.red : Color.green;
        Gizmos.DrawWireSphere(checkCenter, doorBreakRadius);

        if (doorBreakTimer > 0)
        {
            float progress = doorBreakTimer / doorBreakTime;
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawSphere(checkCenter, doorBreakRadius * progress);
        }
    }

    void DrawGizmoCircle(Vector3 center, float radius)
    {
        int segments = 20;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

    Vector3 RoundVector(Vector3 v) => new Vector3(Mathf.Round(v.x), 0, Mathf.Round(v.z)).normalized;
    Vector3 GetGridPosition(Vector3 pos) => new Vector3(Mathf.Round(pos.x / gridSize) * gridSize, 0, Mathf.Round(pos.z / gridSize) * gridSize);
    void SnapToGrid() { Vector3 g = GetGridPosition(transform.position); transform.position = new Vector3(g.x, transform.position.y, g.z); }
    Vector3 SortByLevelPriority(List<Vector3> options)
    {
        Vector3 cur = GetGridPosition(transform.position);
        for (int i = 0; i < options.Count; i++) { Vector3 t = options[i]; int r = UnityEngine.Random.Range(i, options.Count); options[i] = options[r]; options[r] = t; }
        Vector3 best = options[0]; int min = 99;
        foreach (Vector3 d in options) { int v = GetVisitLevel(cur + d * gridSize); if (v < min) { min = v; best = d; } }
        return best;
    }

    IEnumerator PlayHardGlitch()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        float originalAspect = cam.aspect;
        float originalFOV = cam.fieldOfView;
        Vector3 originalLocalPos = cam.transform.localPosition;

        float elapsed = 0f;
        while (elapsed < glitchDuration)
        {
            cam.transform.localPosition = originalLocalPos + UnityEngine.Random.insideUnitSphere * shakeIntensity;
            cam.aspect = originalAspect * UnityEngine.Random.Range(1f / stretchIntensity, stretchIntensity);
            cam.fieldOfView = originalFOV + UnityEngine.Random.Range(-15f, 15f);
            grain.enabled.value = true;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        cam.ResetAspect();
        grain.enabled.value = false;
        cam.fieldOfView = originalFOV;
        cam.transform.localPosition = originalLocalPos;
    }
}