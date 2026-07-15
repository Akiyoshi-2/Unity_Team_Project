using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    [Header("追跡設定")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("見失った後の予測・検索挙動")]
    [SerializeField] private float predictionDistance = 3f; // 最後に見た場所からどれくらい踏み込むか
    [SerializeField] private float searchTime = 4f;
    [SerializeField] private float lookAngle = 60f;
    [SerializeField] private float lookSpeed = 2f;

    [Header("徘徊設定")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float waitTimeAtPoint = 2f;
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();

    [Header("ノイズ演出設定")]
    [SerializeField] private float glitchDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float stretchIntensity = 2.0f;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private Vector3 targetSearchPosition; // 予測を含めた最終目的地

    private enum State { Patrolling, Chasing, Searching }
    private State currentState = State.Patrolling;

    private bool isPlayerVisible = false;
    private float searchTimer = 0f;
    private float patrolTimer = 0f;
    private int currentPatrolIndex = 0;

    private bool isReady = false;

    IEnumerator Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = 0.1f;

        // --- 修正箇所：NavMeshが準備できるまで待機 ---
        // 1. まずは1フレーム待って、FloorスクリプトのGenerateFloorが確実に呼ばれるようにする
        yield return null;

        // 2. NavMeshAgentが有効なNavMeshの上に配置されるまで待機する
        // (NavMeshSurface.BuildNavMeshが完了するまでループ)
        while (!agent.isOnNavMesh)
        {
            // まだベイクが終わっていない場合は少し待機
            yield return new WaitForSeconds(0.1f);
        }

        // 3. マップ生成が終わってからパトロールポイントを探す
        GameObject[] foundPoints = GameObject.FindGameObjectsWithTag("PatrolPoints");
        patrolPoints.Clear();
        foreach (GameObject point in foundPoints) patrolPoints.Add(point.transform);

        FindPlayer();

        // 初期目的地を設定（最初のパトロールポイントへ）
        if (patrolPoints.Count > 0)
        {
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }

        isReady = true; // 準備完了
    }
    void Update()
    {
        // 準備が整っていない場合は何もしない
        if (!isReady) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        CheckVisibility();

        switch (currentState)
        {
            case State.Patrolling:
                PatrolLogic();
                break;
            case State.Chasing:
                ChaseLogic();
                break;
            case State.Searching:
                SearchLogic();
                break;
        }
    }

    void CheckVisibility()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool wasVisible = isPlayerVisible;
        isPlayerVisible = false;

        if (distanceToPlayer <= detectionRange)
        {
            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer);

            if (angleToPlayer < viewAngle / 2f)
            {
                if (!Physics.Raycast(transform.position + Vector3.up, dirToPlayer, distanceToPlayer, obstacleMask))
                {
                    isPlayerVisible = true;

                    if (!wasVisible)
                    {
                        currentState = State.Chasing;
                        agent.isStopped = false;
                        StartCoroutine(PlayHardGlitch());
                    }
                }
            }
        }

        // 追跡中に見失った瞬間の処理
        if (!isPlayerVisible && currentState == State.Chasing)
        {
            currentState = State.Searching;
            searchTimer = 0f;
            agent.isStopped = false;

            // --- 予測地点の計算 ---
            // 敵からプレイヤーへの方向を計算
            Vector3 jumpDir = (playerTransform.position - transform.position).normalized;
            // プレイヤーの位置からさらに predictionDistance 分だけ先に目的地を置く
            Vector3 rawPredictedPos = playerTransform.position + jumpDir * predictionDistance;

            // 予測地点が壁の中だった場合、最も近い「歩ける場所」に補正する
            NavMeshHit hit;
            if (NavMesh.SamplePosition(rawPredictedPos, out hit, predictionDistance + 1f, NavMesh.AllAreas))
            {
                targetSearchPosition = hit.position;
            }
            else
            {
                targetSearchPosition = playerTransform.position;
            }

            agent.SetDestination(targetSearchPosition);
        }
    }

    void ChaseLogic()
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(playerTransform.position);
    }

    void SearchLogic()
    {
        agent.speed = chaseSpeed;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            agent.isStopped = true;
            searchTimer += Time.deltaTime;

            float angle = Mathf.Sin(Time.time * lookSpeed) * lookAngle;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y + angle * Time.deltaTime, 0);

            if (searchTimer >= searchTime)
            {
                agent.isStopped = false;
                currentState = State.Patrolling;
            }
        }
    }

    void PatrolLogic()
    {
        agent.speed = patrolSpeed;
        if (patrolPoints.Count == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            agent.isStopped = true;
            patrolTimer += Time.deltaTime;

            if (patrolTimer >= waitTimeAtPoint)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
                agent.isStopped = false;
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                patrolTimer = 0f;
            }
        }
        else
        {
            agent.isStopped = false;
        }
    }

    // --- Glitch演出コルーチン（以前のまま） ---
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
            cam.transform.localPosition = originalLocalPos + Random.insideUnitSphere * shakeIntensity;
            cam.aspect = originalAspect * Random.Range(1f / stretchIntensity, stretchIntensity);
            cam.fieldOfView = originalFOV + Random.Range(-15f, 15f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        cam.ResetAspect();
        cam.fieldOfView = originalFOV;
        cam.transform.localPosition = originalLocalPos;
        cam.transform.localRotation = originalLocalRot;
    }

    void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Destroy(other.gameObject);
            playerTransform = null;
            currentState = State.Patrolling;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Vector3 leftDir = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + leftDir * detectionRange);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * detectionRange);

        // 予測目的地を青い球で表示（デバッグ用）
        if (currentState == State.Searching)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(targetSearchPosition, 0.5f);
        }
    }
}