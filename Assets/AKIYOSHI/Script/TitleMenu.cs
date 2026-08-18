using UnityEngine;

public class TitleMenu : MonoBehaviour
{
    // 「初めから」
    public void StartNewGame()
    {
        // ステージのクリア情報をリセット
        PlayerPrefs.DeleteKey("EarStage_Cleared");
        PlayerPrefs.DeleteKey("EyeStage_Cleared");
        PlayerPrefs.DeleteKey("MouthStage_Cleared");
        PlayerPrefs.DeleteKey("NoseStage_Cleared");

        // 直前のクリア情報も削除
        PlayerPrefs.DeleteKey("ClearFromScene");

        PlayerPrefs.Save();

        // NarrationSceneへ
        FadeManager.Instance.LoadScene("NarrationScene");
    }

    // 「続きから」
    public void ContinueGame()
    {
        // データをそのまま残してSelectSceneへ
        FadeManager.Instance.LoadScene("SelectScene");
    }
}