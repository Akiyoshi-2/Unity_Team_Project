using UnityEngine;

public class ClockEffect : MonoBehaviour
{
    [Header("時計設定")]
    [SerializeField] private float duration = 15f;

    void Start()
    {
        // 1. 自動的にトリガー設定にする（物理で飛んでいかないように）
        if (TryGetComponent<Collider>(out Collider col))
        {
            col.isTrigger = true;
        }

        if (Player.Instance != null)
        {
            // 2. 座標を敵のグリッド(2.0)にスナップさせる
            float gridSize = 2.0f;
            Vector3 snappedPos = new Vector3(
                Mathf.Round(transform.position.x / gridSize) * gridSize,
                transform.position.y, // 高さは生成時のまま
                Mathf.Round(transform.position.z / gridSize) * gridSize
            );

            transform.position = snappedPos;
            Player.Instance.clockFlg = true;
            Player.Instance.clockPos = snappedPos;

        }

        // 3. 15秒後に消滅
        Destroy(gameObject, duration);
    }

    private void OnDestroy()
    {
        if (Player.Instance != null)
        {
            // 2023.1以降は FindObjectsByType、それ以前は FindObjectsOfType
            ClockEffect[] otherClocks = Object.FindObjectsByType<ClockEffect>(FindObjectsSortMode.None);
            if (otherClocks.Length <= 1)
            {
                Player.Instance.clockFlg = false;
            }
        }
    }
}