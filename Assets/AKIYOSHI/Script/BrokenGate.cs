using UnityEngine;

public class BrokenGate : MonoBehaviour
{
    [SerializeField] private string stageName;

    private void Start()
    {
        // クリア済みならアクティブ
        if (PlayerPrefs.GetInt(stageName + "_Cleared", 0) == 1)
        {
            gameObject.SetActive(true);
        }
        else 
        {
            gameObject.SetActive(false);
        }
    }
}
