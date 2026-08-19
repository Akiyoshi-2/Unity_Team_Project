using UnityEngine;

public class EnemyTPArea : MonoBehaviour
{
    [Header("対象設定")]
    [SerializeField] private string enemyTag = "Enemy";

    private void OnTriggerEnter(Collider other)
    {
        // Enemy本体、またはEnemyの子ColliderからEnemyを探す
        Enemy enemy = other.GetComponentInParent<Enemy>();

        if (enemy == null)
            return;

        // Enemyをスポーン地点へワープ
        enemy.TeleportToSpawn();
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