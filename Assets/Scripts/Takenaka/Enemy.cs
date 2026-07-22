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

        if (Vector3.Distance(transform.position, player.position) < killRange)
        {
            Debug.Log("プレイヤーを捕らえました！");
            Destroy(player.gameObject);
            return;
        }

        bool canSee = CanSeePlayer();

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
            StartCoroutine(PlayHardGlitch());
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
         vignette.enabled.value = false;
        chromaticAberration.enabled.value = false;

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
    // 可視化デバッグ
    void OnDrawGizmos()
    {
        // 1. 訪問レベルのタイル表示 (既存機能)
        if (showVisitLevels && Application.isPlaying)
        {
            foreach (var entry in visitLevelMap)
            {
                int level = entry.Value;
                Color c = Color.cyan; // Level 1
                if (level == 2) c = Color.blue;
                if (level >= 3) c = Color.magenta;

                c.a = 0.3f;
                Gizmos.color = c;
                Gizmos.DrawCube(entry.Key + Vector3.up * 0.1f, new Vector3(gridSize * 0.9f, 0.1f, gridSize * 0.9f));
            }
        }

        // 2. 視野角 (FOV) の可視化
        Vector3 eyePos = transform.position + Vector3.up; // 足元ではなく少し高い位置から表示

        // 状態によって色を変更
        Color fovColor = Color.yellow; // 通常時（Patrol）
        if (currentState == State.Chase) fovColor = Color.red; // 追跡時
        else if (currentState == State.Search) fovColor = new Color(1f, 0.5f, 0f); // 捜索時（オレンジ）

        Gizmos.color = fovColor;

        // 視野の境界線を描画
        Vector3 leftBoundary = Quaternion.Euler(0, -fovAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, fovAngle * 0.5f, 0) * transform.forward;

        Gizmos.DrawRay(eyePos, leftBoundary * detectionRange);
        Gizmos.DrawRay(eyePos, rightBoundary * detectionRange);

        // 扇形の外周を補完する線（より視野らしく見せるため）
        int segments = 10;
        Vector3 prevPoint = eyePos + leftBoundary * detectionRange;
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = -fovAngle * 0.5f + (fovAngle / segments) * i;
            Vector3 nextDir = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
            Vector3 nextPoint = eyePos + nextDir * detectionRange;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        // 3. 現在のターゲットマスの表示 (既存機能)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(currentTargetCell + Vector3.up * 0.1f, new Vector3(gridSize, 0.2f, gridSize));

            // 追跡中、最後に見失った場所も表示
            if (currentState == State.Search)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(lastSeenCell + Vector3.up * 0.5f, 0.5f);
                Gizmos.DrawLine(eyePos, lastSeenCell + Vector3.up * 0.5f);
            }
        }
    }

    // ヘルパー
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
        Quaternion originalLocalRot = cam.transform.localRotation;

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
        cam.transform.localRotation = originalLocalRot;
    }
}