using UnityEngine;
using TMPro;
using System.Collections;

public class GoalText : MonoBehaviour
{
    [SerializeField] private TMP_Text clearText;

    [Header("文字送り速度")]
    [SerializeField] private float textSpeed = 0.05f;

    [Header("文字送り終了後の待ち時間")]
    [SerializeField] private float sceneChangeDelay = 3.0f;

    private string message;
    private string fromScene;

    private void Start()
    {
        fromScene = PlayerPrefs.GetString("ClearFromScene", "");

        switch (fromScene)
        {
            case "EarStage":
                message = "耳を取り戻した";
                break;

            case "EyeStage":
                message = "目を取り戻した";
                break;

            case "MouthStage":
                message = "口を取り戻した";
                break;

            case "NoseStage":
                message = "鼻を取り戻した";
                break;
        }

        clearText.text = "";

        StartCoroutine(TypeText());
    }

    private IEnumerator TypeText()
    {
        // 文字送り
        foreach (char c in message)
        {
            clearText.text += c;

            yield return new WaitForSeconds(textSpeed);
        }

        // 文字送りが終わったらクリア済みにする
        PlayerPrefs.SetInt(fromScene + "_Cleared", 1);
        PlayerPrefs.Save();

        yield return new WaitForSeconds(sceneChangeDelay);

        // SelectSceneへ
        FadeManager.Instance.LoadScene("SelectScene");
    }
}