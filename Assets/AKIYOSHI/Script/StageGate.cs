using UnityEngine;

public class StageGate : MonoBehaviour
{
    [SerializeField] private string stageName;

    private void Start()
    {
        // クリア済みなら非アクティブ
        if (PlayerPrefs.GetInt(stageName + "_Cleared", 0) == 1)
        {
            gameObject.SetActive(false);
        }
    }
}