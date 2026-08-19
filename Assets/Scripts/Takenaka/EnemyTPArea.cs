using UnityEngine;

public class EnemyTPArea : MonoBehaviour
{
    [Header("対象設定")]
    [SerializeField] private string enemyTag = "Enemy";

    private void OnTriggerEnter(Collider other)
    {
        // 1. タグで判定（パフォーマンスと誤作動防止のため）
        if (other.CompareTag(enemyTag))
        {
            // 2. Enemy本体、またはEnemyの子ColliderからEnemyコンポーネントを探す
            Enemy enemy = other.GetComponentInParent<Enemy>();

            if (enemy != null)
            {
                // 3. その敵が持っているスポーン地点へワープ
                Debug.Log($"{other.name} がワープエリアに進入。スポーン地点へ戻します。");
                enemy.TeleportToSpawn();
            }
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
            return;

        Gizmos.color = new Color(1f, 0f, 1f, 0.25f);

        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = new Color(1f, 0f, 1f, 0.8f);

        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = oldMatrix;
    }
}