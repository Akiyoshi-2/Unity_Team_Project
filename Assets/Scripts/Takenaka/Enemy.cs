using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class Enemy : MonoBehaviour
{
    [Header("移動設定")]
    [SerializeField] private float patrolSpeed = 3f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float sensorRange = 10f;

    [Header("中央補正")]
    [SerializeField] private float centerSlideSpeed = 3f;
    [SerializeField] private float deadZone = 0.1f;
    [SerializeField] private float snapSpeed = 15f;

    [Header("判定設定")]
    [SerializeField] private float frontCheckDistance = 1.5f; // 前方の壁判定距離
    [SerializeField] private float sideCheckDistance = 2.0f;  // 左右の壁判定距離
    [SerializeField] private float frontCheckOffset = 1.5f;   // 左右確認用レイの起点オフセット
    [SerializeField] private float branchCheckDistance = 2.5f; // 分岐路（道があるか）の判定距離

    private CharacterController controller;
    private bool isTurning;
    private bool isReady;
    private bool inIntersection = false;

    IEnumerator Start()
    {
        controller = GetComponent<CharacterController>();

        // 開始時の向きをグリッド（90度単位）に合わせる
        SnapRotationImmediate();

        yield return new WaitForSeconds(1f);
        isReady = true;
    }

    void Update()
    {
        if (!isReady) return;

        // 常に前進・探索移動
        AISensingMove(patrolSpeed);
    }

    void AISensingMove(float speed)
    {
        if (isTurning) return;

        // 常に90度単位に姿勢を補正
        SnapRotation();

        // 各方向の距離計測
        float distFront = GetRayDist(transform.forward);
        float distLeft = GetLeftRayAhead();
        float distRight = GetRightRayAhead();

        // 壁があるかどうかの判定
        bool wallFront = distFront < frontCheckDistance;
        bool wallLeft = distLeft < sideCheckDistance;
        bool wallRight = distRight < sideCheckDistance;

        // 中央補正（左右に壁がある時、真ん中を歩く）
        Vector3 slide = Vector3.zero;
        if (wallLeft && wallRight)
        {
            float diff = distLeft - distRight;
            if (Mathf.Abs(diff) > deadZone)
                slide = transform.right * diff * centerSlideSpeed;
        }

        // 分岐路（道が開けているか）の判定
        bool hasLeftPath = HasLeftBranch();
        bool hasRightPath = HasRightBranch();
        bool isAtIntersection = hasLeftPath || hasRightPath;

        // 前が壁、もしくは新しい交差点に差し掛かった場合
        if (wallFront)
        {
            DecideRandomDirection(true, hasLeftPath, hasRightPath);
            return;
        }

        if (isAtIntersection)
        {
            if (!inIntersection)
            {
                inIntersection = true;
                // 交差点での進路決定（前が空いていても、たまに曲がるようにするならここで処理）
                DecideRandomDirection(false, hasLeftPath, hasRightPath);
            }
        }
        else
        {
            inIntersection = false;
        }

        // 移動実行
        Vector3 velocity = transform.forward * speed + slide;
        velocity.y = -9.81f; // 簡易重力
        controller.Move(velocity * Time.deltaTime);
    }

    // 進む方向をランダムに決定する
    void DecideRandomDirection(bool mustTurn, bool canGoLeft, bool canGoRight)
    {
        List<float> availableAngles = new List<float>();

        // 前進できる場合（前が壁でないとき）
        if (!mustTurn)
        {
            availableAngles.Add(0); // そのまま真っ直ぐ
        }

        if (canGoLeft) availableAngles.Add(-90);
        if (canGoRight) availableAngles.Add(90);

        // どこにも行けない（行き止まり）なら180度反転
        if (availableAngles.Count == 0)
        {
            StartCoroutine(SmoothTurn(180));
            return;
        }

        // 選択肢からランダムに選ぶ
        float chosenAngle = availableAngles[Random.Range(0, availableAngles.Count)];

        if (chosenAngle != 0)
        {
            StartCoroutine(SmoothTurn(chosenAngle));
        }
    }

    IEnumerator SmoothTurn(float angle)
    {
        isTurning = true;

        float currentY = Mathf.Round(transform.eulerAngles.y / 90f) * 90f;
        Quaternion startRot = Quaternion.Euler(0, currentY, 0);
        Quaternion endRot = Quaternion.Euler(0, currentY + angle, 0);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 5f;
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        transform.rotation = endRot;
        // 回転後に少しだけ押し出して壁判定の重複を防ぐ
        controller.Move(transform.forward * 0.2f);

        isTurning = false;
    }

    void SnapRotation()
    {
        float targetY = Mathf.Round(transform.eulerAngles.y / 90f) * 90f;
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            Quaternion.Euler(0, targetY, 0),
            Time.deltaTime * snapSpeed);
    }

    void SnapRotationImmediate()
    {
        float targetY = Mathf.Round(transform.eulerAngles.y / 90f) * 90f;
        transform.rotation = Quaternion.Euler(0, targetY, 0);
    }

    float GetRayDist(Vector3 dir)
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, out hit, sensorRange, obstacleMask))
            return hit.distance;
        return sensorRange;
    }

    float GetLeftRayAhead()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + transform.forward * frontCheckOffset + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, -transform.right, out hit, sensorRange, obstacleMask))
            return hit.distance;
        return sensorRange;
    }

    float GetRightRayAhead()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + transform.forward * frontCheckOffset + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, transform.right, out hit, sensorRange, obstacleMask))
            return hit.distance;
        return sensorRange;
    }

    bool HasLeftBranch()
    {
        Vector3 origin = transform.position + transform.forward * frontCheckOffset + Vector3.up * 0.5f;
        return !Physics.Raycast(origin, -transform.right, branchCheckDistance, obstacleMask);
    }

    bool HasRightBranch()
    {
        Vector3 origin = transform.position + transform.forward * frontCheckOffset + Vector3.up * 0.5f;
        return !Physics.Raycast(origin, transform.right, branchCheckDistance, obstacleMask);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 sideOrigin = transform.position + transform.forward * frontCheckOffset + Vector3.up * 0.5f;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * frontCheckDistance);
        Gizmos.DrawRay(sideOrigin, -transform.right * branchCheckDistance);
        Gizmos.DrawRay(sideOrigin, transform.right * branchCheckDistance);
    }
}