using UnityEngine;

public class AllClearGate : MonoBehaviour
{
    private void Start()
    {
        bool earCleared =
            PlayerPrefs.GetInt("EarStage_Cleared", 0) == 1;

        bool eyeCleared =
            PlayerPrefs.GetInt("EyeStage_Cleared", 0) == 1;

        bool mouthCleared =
            PlayerPrefs.GetInt("MouthStage_Cleared", 0) == 1;

        bool noseCleared =
            PlayerPrefs.GetInt("NoseStage_Cleared", 0) == 1;

        // 4ステージすべてクリアしていたら表示
        if (earCleared && eyeCleared && mouthCleared && noseCleared)
        {
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
