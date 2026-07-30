using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public enum SEType
    {
        WALK,
    }

    public enum BGMType
    {

    }

    [SerializeField]
    private int SE_PLAY_MAX = 5;

    [SerializeField]
    private AudioClip[] m_SEClips;

    [SerializeField]
    private AudioClip[] m_BGMClips;

    [UnityEngine.Range(0.0f, 1.0f)]
    public float m_SEVolume;

    [UnityEngine.Range(0.0f, 1.0f)]
    public float m_BGMVolume;

    private List<AudioSource> m_SESourceList = new List<AudioSource>();
    private AudioSource m_BGMSource = null;

    private void Awake()
    {
        for (int i = 0; i < SE_PLAY_MAX; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.volume = m_SEVolume;
            m_SESourceList.Add(source);
        }

        m_BGMSource = gameObject.AddComponent<AudioSource>();
        m_BGMSource.loop = true;
        m_BGMSource.volume = m_BGMVolume;
    }

    public void PlaySE(SEType type)
    {
        int index = (int)type;

        if (index < 0 || index > m_SEClips.Length) return;

        foreach (AudioSource source in m_SESourceList)
        {
            if (!source.isPlaying)
            {
                source.PlayOneShot(m_SEClips[index]);
                break;
            }
        }
    }

    public void PlayBGM(BGMType type)
    {
        int index = (int)type;
        if (index < 0 || index > m_BGMClips.Length) return;

        m_BGMSource.clip = m_BGMClips[index];
        m_BGMSource.Play();
    }

    public void StopBGM()
    {
        m_BGMSource.Stop();
    }
}
