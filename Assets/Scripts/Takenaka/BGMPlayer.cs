using UnityEngine;

public class BGMPlayer : MonoBehaviour
{
    [Header("BGMê›íË")]
    [SerializeField]
    private AudioClip m_BGMClip;

    [SerializeField]
    private bool m_PlayOnStart = true;

    private void Start()
    {
        if (!m_PlayOnStart)
            return;

        SoundManager soundManager = FindFirstObjectByType<SoundManager>();

        soundManager.PlayBGM(m_BGMClip);
    }
}