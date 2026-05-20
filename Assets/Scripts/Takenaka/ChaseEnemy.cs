using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class ChaseEnemy : MonoBehaviour
{
    [Header("í«ê’ê›íË")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private float chaseSpeed = 30f;   // í«ê’éûÇÃë¨ìx

    [Header("úpújê›íË")]
    [SerializeField] private float patrolSpeed = 10f;  // úpújéûÇÃë¨ìx
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

        agent.stoppingDistance = stopDistance;

        // É^ÉOéQè∆Ç…ÇÊÇÈé©ìÆéÊìæ
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

        // ÉvÉåÉCÉÑÅ[Ç™Ç¢Ç»Ç¢èÍçáÅiïﬂÇ‹Ç¶ÇΩå„Åj
        if (playerTransform == null)
        {
            isChasing = false;
            agent.speed = patrolSpeed; // úpújë¨ìxÇ…ê›íË
            if (patrolPoints.Count > 0) Patrol();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= detectionRange)
        {
            // í«ê’íÜ
            isChasing = true;
            agent.speed = chaseSpeed; // í«ê’ë¨ìxÇ…ê›íË
            agent.SetDestination(playerTransform.position);
        }
        else
        {
            // îÕàÕäOÇ≈úpújíÜ
            if (isChasing)
            {
                isChasing = false;
            }
            agent.speed = patrolSpeed; // úpújë¨ìxÇ…ê›íË

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
            Debug.Log("ÉvÉåÉCÉÑÅ[ÇïﬂÇ‹Ç¶Ç‹ÇµÇΩÅI");
            Destroy(other.gameObject);
            playerTransform = null;

            // ïﬂÇ‹Ç¶ÇΩíºå„Ç…ë¨ìxÇúpújópÇ…ñﬂÇ∑
            agent.speed = patrolSpeed;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}