using UnityEngine;

public class DoorBreaker : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private float destroyRange = 3.0f;     // 届く距離
    [SerializeField] private float requiredTime = 2.0f;    // 破壊に必要な秒数
    [SerializeField] private KeyCode interactKey = KeyCode.E; // 操作キー

    private float currentTimer = 0f; // 現在の経過時間
    private GameObject targetObject; // 現在見ているオブジェクト

    void Update()
    {
        // レイ（光線）を前方に飛ばして物体を検知
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        // 前方に何かが当たっているかチェック
        if (Physics.Raycast(ray, out hit, destroyRange))
        {
            // 当たったオブジェクトのタグを確認
            if (hit.collider.CompareTag("Door") || hit.collider.CompareTag("wDoor"))
            {
                // ターゲットを保持
                targetObject = hit.collider.gameObject;

                // 指定のキー（例：Eキー）を押し続けている間
                if (Input.GetKey(interactKey))
                {
                    currentTimer += Time.deltaTime;
                    Debug.Log($"破壊中...残り: {Mathf.Max(0, requiredTime - currentTimer):F2}秒");

                    // 指定時間を超えたら破壊
                    if (currentTimer >= requiredTime)
                    {
                        Destroy(targetObject);
                        currentTimer = 0f;
                        Debug.Log("破壊完了！");
                    }
                }
                else
                {
                    // キーを離したらタイマーリセット
                    currentTimer = 0f;
                }
                return; // 条件に一致した場合はここで終了
            }
        }

        // 何も見ていない、またはタグが違う場合はタイマーリセット
        currentTimer = 0f;
        targetObject = null;
    }

    // デバッグ用にエディタ上で射程距離を可視化（任意）
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * destroyRange);
    }
}