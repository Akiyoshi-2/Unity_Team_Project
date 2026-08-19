using System.Collections;
using UnityEngine;

public class TitleMenu : MonoBehaviour
{
    // BGMをフェードアウトする時間
    [SerializeField]
    private float m_BGMFadeTime = 2.0f;

    private bool m_IsLoading = false;

    // 「初めから」
    public void StartNewGame()
    {
        if (m_IsLoading)
            return;

        // ステージのクリア情報をリセット
        PlayerPrefs.DeleteKey("EarStage_Cleared");
        PlayerPrefs.DeleteKey("EyeStage_Cleared");
        PlayerPrefs.DeleteKey("MouthStage_Cleared");
        PlayerPrefs.DeleteKey("NoseStage_Cleared");

        // 直前のクリア情報も削除
        PlayerPrefs.DeleteKey("ClearFromScene");

        PlayerPrefs.Save();

        StartCoroutine(LoadSceneWithBGMFade("NarrationScene"));
    }

    // 「続きから」
    public void ContinueGame()
    {
        if (m_IsLoading)
            return;

        StartCoroutine(LoadSceneWithBGMFade("SelectScene"));
    }

    private IEnumerator LoadSceneWithBGMFade(string sceneName)
    {
        m_IsLoading = true;

        SoundManager soundManager = FindFirstObjectByType<SoundManager>();

        if (soundManager != null)
        {
            soundManager.FadeOutBGM(m_BGMFadeTime);

            // BGMが完全に消えるまで待つ
            yield return new WaitForSeconds(m_BGMFadeTime);
        }

        // BGMフェードアウト完了後にシーン遷移
        FadeManager.Instance.LoadScene(sceneName);
    }
}