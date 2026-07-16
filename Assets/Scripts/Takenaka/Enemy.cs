using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    [Header("追跡設定")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("探索設定")]
    [SerializeField] private float predictionDistance = 3f;
    [SerializeField] private float searchTime = 4f;
    [SerializeField] private float lookAngle = 60f;
    [SerializeField] private float lookSpeed = 2f;

    [Header("巡回設定")]
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float waitTimeAtPoint = 2f;
    [SerializeField] private float rotateSpeed = 5f;

    [Header("中央補正")]
    [SerializeField] private float wallCheckDistance = 5f;
    [SerializeField] private float centerAdjustSpeed = 2f;

    [Header("ノイズ演出")]
    [SerializeField] private float glitchDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float stretchIntensity = 2.0f;

    private enum State
    {
        Patrolling,
        Chasing,
        Searching
    }

    private State currentState = State.Patrolling;

    private Transform playerTransform;

    private List<Transform> patrolPoints = new List<Transform>();

    private Transform currentPatrolPoint;

    private bool isPlayerVisible = false;

    private bool isReady = false;

    private float patrolTimer;

    private float searchTimer;

    private Vector3 targetSearchPosition;

    [SerializeField] private float forwardCheckDistance = 3f;
    [SerializeField] private float turnAngle = 90f;

    private Vector3 moveDirection;

    private CharacterController controller;

    private bool isAvoidingWall = false;
    private Vector3 avoidDirection;

    IEnumerator Start()
    {

        controller =
        GetComponent<CharacterController>();

        Debug.Log("Start開始");

        yield return null;

        Debug.Log("1フレーム経過");

        GameObject[] points =
            GameObject.FindGameObjectsWithTag("PatrolPoints");

        patrolPoints.Clear();

        foreach (GameObject p in points)
        {
            patrolPoints.Add(p.transform);
        }

        FindPlayer();

        FindNearestPatrolPoint();

        moveDirection = transform.forward;

        isReady = true;
    }

    void Update()
    {
        if (!isReady)
            return;

        if (playerTransform == null)
        {
            FindPlayer();
        }

        if (playerTransform != null)
        {
            CheckVisibility();
        }

        switch (currentState)
        {
            case State.Patrolling:
                PatrolLogic();
                break;

            case State.Chasing:
                if (playerTransform != null)
                    ChaseLogic();
                break;

            case State.Searching:
                SearchLogic();
                break;
        }

        KeepCenter();
    }
    bool IsWallAhead()
    {
        Vector3 origin =
            transform.position +
            Vector3.up * 0.5f;

        return Physics.Raycast(
            origin,
            moveDirection.normalized,
            forwardCheckDistance,
            obstacleMask);
    }

    bool CanMove(Vector3 dir)
    {
        Vector3 origin =
            transform.position +
            Vector3.up * 0.5f;

        return !Physics.Raycast(
            origin,
            dir.normalized,
            forwardCheckDistance,
            obstacleMask);
    }

    void ChooseDirection()
    {
        List<Vector3> directions = new List<Vector3>();

        if (CanMove(moveDirection))
            directions.Add(moveDirection);

        if (CanMove(Quaternion.Euler(0, turnAngle, 0) * moveDirection))
            directions.Add(Quaternion.Euler(0, turnAngle, 0) * moveDirection);

        if (CanMove(Quaternion.Euler(0, -turnAngle, 0) * moveDirection))
            directions.Add(Quaternion.Euler(0, -turnAngle, 0) * moveDirection);
        if (directions.Count == 0)
        {
            moveDirection = -moveDirection;
            return;
        }

        moveDirection =
            directions[Random.Range(0, directions.Count)];
    }

    void FindPlayer()
    {
        GameObject obj =
            GameObject.FindGameObjectWithTag("Player");

        if (obj != null)
        {
            playerTransform = obj.transform;
        }
    }

    void FindNearestPatrolPoint()
    {
        float min = Mathf.Infinity;

        foreach (Transform point in patrolPoints)
        {
            float dist =
                Vector3.Distance(
                    transform.position,
                    point.position);

            if (dist < min)
            {
                min = dist;
                currentPatrolPoint = point;
            }
        }
    }

    void CheckVisibility()
    {
        float distance =
            Vector3.Distance(
                transform.position,
                playerTransform.position);

        bool wasVisible = isPlayerVisible;

        isPlayerVisible = false;

        if (distance <= detectionRange)
        {
            Vector3 dir =
                (playerTransform.position -
                transform.position).normalized;

            float angle =
                Vector3.Angle(transform.forward, dir);

            if (angle <= viewAngle * 0.5f)
            {
                if (!Physics.Raycast(
                    transform.position + Vector3.up,
                    dir,
                    distance,
                    obstacleMask))
                {
                    isPlayerVisible = true;

                    if (!wasVisible)
                    {
                        currentState = State.Chasing;
                        StartCoroutine(
                            PlayHardGlitch());
                    }
                }
            }
        }

        if (!isPlayerVisible &&
           currentState == State.Chasing)
        {
            currentState = State.Searching;

            searchTimer = 0f;

            Vector3 dir =
                (playerTransform.position -
                transform.position).normalized;

            targetSearchPosition =
                playerTransform.position +
                dir * predictionDistance;
        }
    }
    void ChaseLogic()
    {
        Vector3 toPlayer =
            playerTransform.position - transform.position;

        toPlayer.y = 0;

        if (toPlayer.sqrMagnitude < 0.01f)
            return;

        if (!isAvoidingWall)
        {
            moveDirection = toPlayer.normalized;

            if (IsWallAhead())
            {
                ChooseDirectionTowards(playerTransform.position);

                avoidDirection = moveDirection;

                isAvoidingWall = true;
            }
        }
        else
        {
            moveDirection = avoidDirection;

            if (!IsWallAhead())
            {
                isAvoidingWall = false;
            }
        }

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDirection),
                rotateSpeed * Time.deltaTime);

        controller.Move(
            moveDirection *
            chaseSpeed *
            Time.deltaTime);
    }
    void PatrolLogic()
    {
        if (currentPatrolPoint == null)
            return;

        Vector3 toTarget =
            currentPatrolPoint.position - transform.position;

        toTarget.y = 0;

        if (toTarget.magnitude < 0.5f)
        {
            currentPatrolPoint =
                GetRandomPatrolPoint(currentPatrolPoint);

            isAvoidingWall = false;

            return;
        }

        if (!isAvoidingWall)
        {
            moveDirection = toTarget.normalized;

            if (IsWallAhead())
            {
                ChooseDirectionTowards(currentPatrolPoint.position);

                avoidDirection = moveDirection;

                isAvoidingWall = true;
            }
        }
        else
        {
            moveDirection = avoidDirection;

            if (!IsWallAhead())
            {
                isAvoidingWall = false;
            }
        }

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDirection),
                rotateSpeed * Time.deltaTime);

        controller.Move(
            moveDirection *
            patrolSpeed *
            Time.deltaTime);
    }

    Transform GetRandomPatrolPoint(Transform current)
    {
        if (patrolPoints.Count == 0)
            return null;

        if (patrolPoints.Count == 1)
            return patrolPoints[0];

        Transform next;

        do
        {
            next =
                patrolPoints[
                    Random.Range(
                        0,
                        patrolPoints.Count)];
        }
        while (next == current);

        return next;
    }

    void SearchLogic()
    {
        Vector3 toTarget =
            targetSearchPosition -
            transform.position;

        toTarget.y = 0;

        if (!isAvoidingWall)
        {
            moveDirection = toTarget.normalized;

            if (IsWallAhead())
            {
                ChooseDirectionTowards(targetSearchPosition);

                avoidDirection = moveDirection;

                isAvoidingWall = true;
            }
        }
        else
        {
            moveDirection = avoidDirection;

            if (!IsWallAhead())
            {
                isAvoidingWall = false;
            }
        }
    }

    void KeepCenter()
    {
        RaycastHit leftHit;
        RaycastHit rightHit;

        bool left =
            Physics.Raycast(
                transform.position,
                -transform.right,
                out leftHit,
                wallCheckDistance,
                obstacleMask);

        bool right =
            Physics.Raycast(
                transform.position,
                transform.right,
                out rightHit,
                wallCheckDistance,
                obstacleMask);

        if (left && right)
        {
            float diff =
                leftHit.distance -
                rightHit.distance;

            transform.position +=
                transform.right *
                diff *
                centerAdjustSpeed *
                Time.deltaTime;
        }
    }

    IEnumerator PlayHardGlitch()
    {
        Camera cam = Camera.main;

        if (cam == null)
            yield break;

        float originalAspect =
            cam.aspect;

        float originalFOV =
            cam.fieldOfView;

        Vector3 originalPos =
            cam.transform.localPosition;

        Quaternion originalRot =
            cam.transform.localRotation;

        float elapsed = 0f;

        while (elapsed < glitchDuration)
        {
            cam.transform.localPosition =
                originalPos +
                Random.insideUnitSphere *
                shakeIntensity;

            cam.aspect =
                originalAspect *
                Random.Range(
                   1f / stretchIntensity,
                stretchIntensity);

            cam.fieldOfView =
                originalFOV +
                Random.Range(-15f, 15f);

            elapsed += Time.unscaledDeltaTime;

            yield return null;
        }

        cam.ResetAspect();
        cam.fieldOfView = originalFOV;
        cam.transform.localPosition = originalPos;
        cam.transform.localRotation = originalRot;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Destroy(other.gameObject);

            playerTransform = null;

            currentState = State.Patrolling;

            FindNearestPatrolPoint();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange);

        Vector3 leftDir =
            Quaternion.Euler(
                0,
                -viewAngle * 0.5f,
                0) *
            transform.forward;

        Vector3 rightDir =
            Quaternion.Euler(
                0,
                viewAngle * 0.5f,
                0) *
            transform.forward;

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            leftDir *
            detectionRange);

        Gizmos.DrawLine(
            transform.position,
            transform.position +
            rightDir *
            detectionRange);

        Gizmos.color = Color.blue;

        Gizmos.DrawSphere(
            targetSearchPosition,
            0.3f);

        if (currentPatrolPoint != null)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawLine(
                transform.position,
                currentPatrolPoint.position);

            Gizmos.DrawSphere(
                currentPatrolPoint.position,
                0.4f);
        }

        // 左右の壁チェック用Ray
        Gizmos.color = Color.yellow;

        Gizmos.DrawRay(
            transform.position,
            transform.right *
            wallCheckDistance);

        Gizmos.DrawRay(
            transform.position,
            -transform.right *
            wallCheckDistance);

        // 前方確認Ray
        Gizmos.color = Color.cyan;

        Gizmos.DrawRay(
            transform.position,
            transform.forward *
            wallCheckDistance);
    }

    void ChooseDirectionTowards(Vector3 targetPosition)
    {
        List<Vector3> candidates = new List<Vector3>();

        Vector3 forward = moveDirection.normalized;
        Vector3 right = transform.right;
        Vector3 left = -transform.right;
        Vector3 back = -forward;

        if (CanMove(forward))
            candidates.Add(forward);

        if (CanMove(right))
            candidates.Add(right);

        if (CanMove(left))
            candidates.Add(left);

        if (CanMove(back))
            candidates.Add(back);

        if (candidates.Count == 0)
            return;

        Vector3 bestDir = candidates[0];
        float bestDistance = Mathf.Infinity;

        foreach (Vector3 dir in candidates)
        {
            Vector3 nextPos =
                transform.position +
                dir * 2f;

            float distance =
                Vector3.Distance(
                    nextPos,
                    targetPosition);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestDir = dir;
            }
        }

        moveDirection = bestDir;
    }

}
