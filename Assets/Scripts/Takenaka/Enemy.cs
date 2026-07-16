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

    IEnumerator Start()
    {
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
        return Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            moveDirection,
            forwardCheckDistance,
            obstacleMask);
    }

    bool CanMove(Vector3 dir)
    {
        return !Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            dir,
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
        Vector3 dir =
            playerTransform.position -
            transform.position;

        dir.y = 0;

        // プレイヤーが真上・真下にいる場合は何もしない
        if (dir.sqrMagnitude < 0.01f)
            return;

        // 進行方向を更新
        moveDirection = dir.normalized;

        // 前方が壁なら進める方向を探す
        if (IsWallAhead())
        {
            ChooseDirection();
        }

        // 向きを滑らかに変更
        Quaternion targetRot =
            Quaternion.LookRotation(moveDirection);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotateSpeed * Time.deltaTime);

        // 前進
        transform.position +=
            moveDirection *
            chaseSpeed *
            Time.deltaTime;
    }

    void PatrolLogic()
    {
        if (currentPatrolPoint == null)
            return;

        Vector3 dir =
            currentPatrolPoint.position -
            transform.position;

        dir.y = 0;

        // 到着判定
        if (dir.magnitude < 0.5f)
        {
            currentPatrolPoint =
                GetRandomPatrolPoint(currentPatrolPoint);

            return;
        }

        moveDirection = dir.normalized;

        // 前方が壁なら方向変更
        if (IsWallAhead())
        {
            ChooseDirection();
        }

        Quaternion targetRot =
            Quaternion.LookRotation(moveDirection);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotateSpeed * Time.deltaTime);

        transform.position +=
            moveDirection *
            patrolSpeed *
            Time.deltaTime;
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
        Vector3 dir =
            targetSearchPosition -
            transform.position;

        dir.y = 0;

        float dist = dir.magnitude;

        // まだ探索地点に着いていない
        if (dist > 0.2f)
        {
            moveDirection = dir.normalized;

            // 前方に壁があれば別の方向を探す
            if (IsWallAhead())
            {
                ChooseDirection();
            }

            // 向きを滑らかに変更
            Quaternion targetRot =
                Quaternion.LookRotation(moveDirection);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    rotateSpeed * Time.deltaTime);

            // 前進
            transform.position +=
                moveDirection *
                chaseSpeed *
                Time.deltaTime;
        }
        else
        {
            // 探索地点に着いたら周囲を見渡す
            searchTimer += Time.deltaTime;

            float angle =
                Mathf.Sin(
                    Time.time *
                    lookSpeed)
                * lookAngle;

            transform.Rotate(
                0,
                angle * Time.deltaTime,
                0);

            if (searchTimer >= searchTime)
            {
                searchTimer = 0f;

                currentState = State.Patrolling;

                FindNearestPatrolPoint();
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
}
