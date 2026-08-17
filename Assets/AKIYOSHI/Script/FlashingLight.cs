using System;
using UnityEngine;

public class FlashingLight : MonoBehaviour
{
    public Light flashingLight;
    public float flashingDistance = 10.0f;

    // Enemyとの距離を確認する間隔
    [SerializeField]
    private float enemyCheckInterval = 0.1f;

    private float timer;
    private float nextBlinkTime;

    // Enemyとの距離確認用タイマー
    private float enemyCheckTimer;

    // 一番近いEnemyが範囲内にいるか
    private bool enemyNearby;

    // Enemyを最初に一度だけ取得
    private GameObject[] enemies;

    [NonSerialized]
    public bool LightOn = false;

    void Start()
    {
        SetNextBlink();

        // シーン内のEnemyを最初に一度だけ取得
        enemies = GameObject.FindGameObjectsWithTag("Enemy");

        // 最初に一度だけ距離を確認
        CheckEnemyDistance();
    }

    void Update()
    {
        if (flashingLight == null)
            return;
        // ライトOFF
        if (!LightOn)
        {
            flashingLight.enabled = false;

            timer = 0f;
            enemyCheckTimer = 0f;

            return;
        }

        // Enemyとの距離確認
        // 0.1秒に1回だけ実行
        enemyCheckTimer += Time.deltaTime;

        if (enemyCheckTimer >= enemyCheckInterval)
        {
            enemyCheckTimer = 0f;

            CheckEnemyDistance();
        }

        // Enemyが近くにいる
        if (enemyNearby)
        {
            timer += Time.deltaTime;

            if (timer >= nextBlinkTime)
            {
                flashingLight.enabled = !flashingLight.enabled;

                flashingLight.intensity =
                    UnityEngine.Random.Range(0.5f, 2.5f);

                timer = 0f;

                SetNextBlink();
            }
        }
        else
        {
            // Enemyが遠い場合
            flashingLight.enabled = true;
            flashingLight.intensity = 2f;

            timer = 0f;
        }
    }

    // Enemyとの距離を確認
    private void CheckEnemyDistance()
    {
        if (enemies == null || enemies.Length == 0)
        {
            enemyNearby = false;
            return;
        }

        float checkDistanceSqr =
            flashingDistance * flashingDistance;

        enemyNearby = false;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null)
                continue;

            Vector3 diff =
                transform.position - enemy.transform.position;

            float distanceSqr = diff.sqrMagnitude;

            if (distanceSqr <= checkDistanceSqr)
            {
                enemyNearby = true;
                break;
            }
        }
    }

    // 次の点滅タイミングを決める
    private void SetNextBlink()
    {
        nextBlinkTime =
            UnityEngine.Random.Range(0.01f, 0.4f);
    }
}