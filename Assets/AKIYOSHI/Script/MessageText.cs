using TMPro;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class MessageText : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;

    [TextArea(3, 10)]
    [SerializeField] private string[] messages;

    // 文字送り速度
    [SerializeField] private float textSpeed = 0.05f;

    // 次のシーン
    [SerializeField] private string nextSceneName = "GameScene";

    // 待機時間
    [SerializeField] private float sceneChangeDelay = 0.5f;

    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private bool isEnding = false;
    private int currentMessage = 0;
    private bool canClick = false;

    private void Start()
    {
        if (messages.Length > 0)
        {
            typingCoroutine = StartCoroutine(TypeText(messages[currentMessage]));
        }

        StartCoroutine(EnableClick());
    }

    IEnumerator TypeText(string text)
    {
        isTyping = true;
        messageText.text = "";

        foreach (char c in text)
        {
            messageText.text += c;
            yield return new WaitForSeconds(textSpeed);
        }

        isTyping = false;
    }

    IEnumerator EnableClick()
    {
        yield return null;      // 1フレーム待つ
        canClick = true;
    }

    IEnumerator ChangeScene()
    {
        yield return new WaitForSeconds(sceneChangeDelay);

        // フェード付きでシーン遷移
        FadeManager.Instance.LoadScene(nextSceneName);
    }

    private void Update()
    {
        if (!canClick || isEnding)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                StopCoroutine(typingCoroutine);
                messageText.text = messages[currentMessage];
                isTyping = false;
            }
            else
            {
                currentMessage++;

                if (currentMessage < messages.Length)
                {
                    typingCoroutine = StartCoroutine(TypeText(messages[currentMessage]));
                }
                else
                {
                    isEnding = true;
                    StartCoroutine(ChangeScene());
                }
            }
        }
    }
}
