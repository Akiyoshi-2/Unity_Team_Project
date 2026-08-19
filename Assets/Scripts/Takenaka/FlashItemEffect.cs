using UnityEngine;
using System.Collections;

public class FlashItemEffect : MonoBehaviour
{
    [Header("フラッシュ設定")]
    [SerializeField] private float flashRange = 20f;
    [SerializeField] private float stunDuration = 10f; // 敵が止まり、当たり判定が消える時間

    void Start()
    {
        // 1. フラッシュ実行（敵の無効化処理）
        ExecuteFlash();

        // 2. 全ての敵の復帰を待ってから、このエフェクト管理オブジェクトを消す
        Destroy(gameObject, stunDuration + 0.5f);
    }

    private void ExecuteFlash()
    {
        // シーン内の「Enemy」タグが付いたオブジェクトをすべて取得
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject obj in enemies)
        {
            Enemy enemy = obj.GetComponent<Enemy>();
            if (enemy != null)
            {
                // プレイヤー（このオブジェクト）と敵の距離を計算
                float dist = Vector3.Distance(transform.position, enemy.transform.position);

                if (dist <= flashRange)
                {
                    // 範囲内の敵に対してスタン（無効化）コルーチンを開始
                    StartCoroutine(StunEnemyRoutine(enemy));
                }
            }
        }
    }

    private IEnumerator StunEnemyRoutine(Enemy enemy)
    {
        // Enemy.cs の既存フラグをONにする
        // これにより Enemy.cs 内で移動停止とコライダーOFFが実行されます
        enemy.flashLightHit = true;

        // 指定時間待機
        yield return new WaitForSeconds(stunDuration);

        // 敵がまだ存在していれば、フラグを戻して復帰させる
        if (enemy != null)
        {
            enemy.flashLightHit = false;
        }
    }
}