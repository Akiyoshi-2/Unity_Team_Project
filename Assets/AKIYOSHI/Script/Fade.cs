using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Fade : MonoBehaviour
{
    enum State
    {
        NONE,
        FADEIN,
        FADEOUT
    }

    Image m_FadeImage = null;

    private float m_FadeTime = 0.0f;

    Coroutine m_FadeInCoroutine = null;
    Coroutine m_FadeOutCoroutine = null;

    State m_State = State.NONE;

    private void Start()
    {
        m_FadeImage = GetComponent<Image>();
    }

    public void StartFadeIn(float fadeTime)
    {
        if (m_State == State.FADEIN) return;

        if (m_FadeOutCoroutine != null)
        {
            StopCoroutine(m_FadeOutCoroutine);
        }

        m_FadeTime = fadeTime;

        m_State = State.FADEIN;

        IEnumerator coroutine = FadeIn();
        m_FadeInCoroutine = StartCoroutine(coroutine);

    }

    public void StartFadeOut(float fadeTime)
    {
        if(m_State == State.FADEOUT) return;

        if (m_FadeInCoroutine != null)
        {
            StopCoroutine(m_FadeInCoroutine);
        }

        m_FadeTime = fadeTime;

        m_State = State.FADEOUT;

        IEnumerator coroutine = FadeOut();
        m_FadeInCoroutine = StartCoroutine(coroutine);
    }

    IEnumerator FadeIn()
    {
        UnityEngine.Color color;

        while (m_FadeImage.color.a < 1.0f)
        {
            color = m_FadeImage.color;
            color.a += Time.deltaTime / m_FadeTime;
            m_FadeImage.color = color;

            yield return null;
        }

        color = m_FadeImage.color;
        color.a = 1.0f;
        m_FadeImage.color = color;

        m_State = State.NONE;
    }

    IEnumerator FadeOut()
    {
        UnityEngine.Color color;

        while (m_FadeImage.color.a > 0.0f)
        {
            color = m_FadeImage.color;
            color.a -= Time.deltaTime / m_FadeTime;
            m_FadeImage.color = color;

            yield return null;
        }

        color = m_FadeImage.color;
        color.a = 0.0f;
        m_FadeImage.color = color;

        m_State = State.NONE;
    }
}
