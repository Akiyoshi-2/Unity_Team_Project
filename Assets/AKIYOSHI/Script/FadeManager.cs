using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance;

    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeOutTime = 1.2f;
    [SerializeField] private float fadeInTime = 1.5f;
    [SerializeField] private float blackWaitTime = 0.3f;

    private bool isFading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // Canvasごと残す
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            Destroy(transform.root.gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(Fade(1f, 0f, fadeInTime));
    }

    public void LoadScene(string sceneName)
    {
        if (!isFading)
        {
            StartCoroutine(FadeRoutine(sceneName));
        }
    }

    IEnumerator FadeRoutine(string sceneName)
    {
        isFading = true;

        yield return Fade(0f, 1f, fadeOutTime);

        yield return new WaitForSeconds(blackWaitTime);

        yield return SceneManager.LoadSceneAsync(sceneName);

        yield return new WaitForSeconds(blackWaitTime);

        yield return Fade(1f, 0f, fadeInTime);

        isFading = false;
    }

    IEnumerator Fade(float start, float end, float time)
    {
        float t = 0;
        Color color = fadeImage.color;

        while (t < time)
        {
            t += Time.deltaTime;

            float x = Mathf.Clamp01(t / time);

            // なめらかなイージング
            x = Mathf.SmoothStep(0f, 1f, x);

            color.a = Mathf.Lerp(start, end, x);

            fadeImage.color = color;

            yield return null;
        }

        color.a = end;
        fadeImage.color = color;
    }
}