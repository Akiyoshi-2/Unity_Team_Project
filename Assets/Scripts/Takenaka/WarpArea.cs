using UnityEngine;

public class WarpArea : MonoBehaviour
{
    [Header("この範囲内がワープ候補になります")]
    public BoxCollider areaCollider;

    private void Awake()
    {
        if (areaCollider == null) areaCollider = GetComponent<BoxCollider>();
        // 衝突はさせないのでTriggerにしておく
        if (areaCollider != null) areaCollider.isTrigger = true;
    }

    // デバッグ用：エディタで見やすいように色をつける
    private void OnDrawGizmos()
    {
        if (areaCollider == null) return;
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Gizmos.DrawCube(transform.position + areaCollider.center, areaCollider.size);
    }
}