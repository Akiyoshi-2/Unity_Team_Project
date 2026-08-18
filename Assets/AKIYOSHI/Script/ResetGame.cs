using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    public void ResetData()
    {
        PlayerPrefs.DeleteKey("EarStage_Cleared");
        PlayerPrefs.DeleteKey("EyeStage_Cleared");
        PlayerPrefs.DeleteKey("MouthStage_Cleared");
        PlayerPrefs.DeleteKey("NoseStage_Cleared");

        // 直前のクリアステージ情報も削除
        PlayerPrefs.DeleteKey("ClearFromScene");

        PlayerPrefs.Save();

        Debug.Log("ゲームデータをリセットしました");
    }
}
