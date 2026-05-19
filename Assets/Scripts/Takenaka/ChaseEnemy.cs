using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class ChaseEnemy : MonoBehaviour
{
    [Header("追跡設定")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private float chaseSpeed = 30f;   // 追跡時の速度（インスペクターで調整）

    [Header("徘徊設定")]
    [SerializeField] private float patrolSpeed = 10f;  // 徘徊時の速度（インスペクターで調整）
    [SerializeField] private float waitTimeAtPoint = 2f;
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();

    private NavMeshAgent agent;
    private Transform playerTransform;
    private int currentPatrolIndex = 0;
    private float patrolTimer = 0f;
    private bool isChasing = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // インスペクターの設定を優先するため、初期値のみスクリプトから設定
        agent.stoppingDistance = stopDistance;

        // タグ参照による自動取得
        GameObject[] foundPoints = GameObject.FindGameObjectsWithTag("PatrolPoints");
        patrolPoints.Clear();
        foreach (GameObject point in foundPoints)
        {
            patrolPoints.Add(point.transform);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        // プレイヤーがいない場合（捕まえた後など）
        if (playerTransform == null)
        {
            isChasing = false;
            agent.speed = patrolSpeed; // 徘徊速度に設定
            if (patrolPoints.Count > 0) Patrol();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= detectionRange)
        {
            // 追跡中
            isChasing = true;
            agent.speed = chaseSpeed; // 追跡速度に設定
            agent.SetDestination(playerTransform.position);
        }
        else
        {
            // 範囲外で徘徊中
            if (isChasing)
            {
                isChasing = false;
            }
            agent.speed = patrolSpeed; // 徘徊速度に設定

            if (patrolPoints.Count > 0)
            {
                Patrol();
            }
        }
    }

    void Patrol()
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("プレイヤーを捕まえました！");
            Destroy(other.gameObject);
            playerTransform = null;

            // 捕まえた直後に速度を徘徊用に戻す
            agent.speed = patrolSpeed;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}