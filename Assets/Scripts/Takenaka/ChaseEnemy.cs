using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;


// 追跡と徘徊の切り替えを実装したChaseEnemyクラス
// - プレイヤーが検出範囲内にいる場合は追跡し、いない場合は徘徊する
// - 徘徊ポイントはタグ「PatrolPoints」を持つオブジェクトから自動的に取得
// - プレイヤーに触れたときにプレイヤーを破壊して捕まえたことを示す
// - デバッグ用に検出範囲を赤いワイヤースフィアで表示

[RequireComponent(typeof(NavMeshAgent))]
public class ChaseEnemy : MonoBehaviour
{
    [Header("追跡設定")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float stopDistance = 0.1f;
    [SerializeField] private float chaseSpeed = 30f;   // 追跡時の速度

    [Header("徘徊設定")]
    [SerializeField] private float patrolSpeed = 10f;  // 徘徊時の速度
    [SerializeField] private float waitTimeAtPoint = 2f;
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();

    private NavMeshAgent agent;
    private Transform playerTransform;
    private int currentPatrolIndex = 0;
    private float patrolTimer = 0f;
    private bool isChasing = false;

    void Start()
    {
        // NavMeshAgentの取得
        agent = GetComponent<NavMeshAgent>();

        // 初期速度を徘徊速度に設定
        agent.stoppingDistance = stopDistance;

        // タグ参照による自動取得
        GameObject[] foundPoints = GameObject.FindGameObjectsWithTag("PatrolPoints");

        // 既存のポイントをクリアしてから追加
        patrolPoints.Clear();

        // 見つかったポイントをリストに追加
        foreach (GameObject point in foundPoints)
        {
            patrolPoints.Add(point.transform);
        }

        // プレイヤーの初期位置を取得
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // プレイヤーが見つかった場合のみTransformを取得
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {

        // プレイヤーがいない場合（初期化失敗や捕まえた後）
        if (playerTransform == null)
        {
            // タグ参照による自動取得を試みる
            GameObject p = GameObject.FindGameObjectWithTag("Player");

            // プレイヤーが見つかった場合のみTransformを取得
            if (p != null) playerTransform = p.transform;
        }

        // プレイヤーがいない場合（捕まえた後）
        if (playerTransform == null)
        {
            // 追跡を停止して徘徊に戻る
            isChasing = false;

            // 速度を徘徊用に戻す
            agent.speed = patrolSpeed; // 徘徊速度に設定

            // 徘徊ポイントがある場合は徘徊を続ける
            if (patrolPoints.Count > 0) Patrol();
            return;
        }

        // プレイヤーとの距離を計算
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);


        // プレイヤーが検出範囲内にいるかどうかをチェック
        if (distanceToPlayer <= detectionRange)
        {
            // 追跡中
            isChasing = true;

            // 速度を追跡用に設定
            agent.speed = chaseSpeed; // 追跡速度に設定

            // プレイヤーに向かって移動
            agent.SetDestination(playerTransform.position);
        }
       else
        {
            // 範囲外で徘徊中
            if (isChasing)
            {
                // 追跡から徘徊に切り替える際の処理
                isChasing = false;
            }

            // 速度を徘徊用に戻す
            agent.speed = patrolSpeed; // 徘徊速度に設定

            // 徘徊ポイントがある場合は徘徊を続ける
            if (patrolPoints.Count > 0)
            {

                // 追跡から徘徊に切り替える際の処理
                Patrol();
            }
        }
    }

    void Patrol()
    {

        // 現在の目的地がない場合、または目的地に到達した場合に次のポイントを設定
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            // 次のポイントに向かう前に待機
            patrolTimer += Time.deltaTime;

            // 待機時間が経過したら次のポイントに移動
            if (patrolTimer >= waitTimeAtPoint)
            {
                // 次のポイントに移動
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;

                // 目的地を設定
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);

                // タイマーをリセット
                patrolTimer = 0f;
            }
        }
    }

    // プレイヤーに触れたときの処理
    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーに触れた場合の処理
        if (other.CompareTag("Player"))
        {
            Debug.Log("プレイヤーを捕まえました！");

            // プレイヤーを破壊して捕まえたことを示す
            Destroy(other.gameObject);

            // プレイヤーを捕まえた後は追跡を停止して徘徊に戻る
            playerTransform = null;

            // 捕まえた直後に速度を徘徊用に戻す
            agent.speed = patrolSpeed;
        }
    }

    // デバッグ用に検出範囲を表示
    private void OnDrawGizmosSelected()
    {
        // 検出範囲を赤いワイヤースフィアで表示
        Gizmos.color = Color.red;

        // 現在の位置を中心に検出範囲の半径でワイヤースフィアを描画
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
