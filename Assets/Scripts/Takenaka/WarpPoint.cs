using UnityEngine;

public class WarpPoint : MonoBehaviour
{
    [SerializeField] private Color debugColor = Color.cyan;

    // エディタのシーン画面で見やすくするための表示
    private void OnDrawGizmos()
    {
        Gizmos.color = debugColor;
        // 地面に埋まらないよう少し浮かせて球を表示
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
        // 中心点に小さな塗りつぶし球を表示
        Gizmos.DrawSphere(transform.position + Vector3.up * 0.5f, 0.1f);
    }

    // インスペクターで値を変更したときなどに実行される（設定ミス防止用）
    private void OnValidate()
    {
        // 念のため、タグが設定されていなければ警告を出す
        if (!CompareTag("Warp Point"))
        {
            // 自動でタグを書き換えることはできない（タグが存在しない可能性があるため）のでログで通知
            Debug.LogWarning($"{name} に WarpPoint スクリプトがありますが、Tag が 'Warp Point' になっていません！");
        }
    }
}