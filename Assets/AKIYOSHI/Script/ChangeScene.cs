using UnityEngine;

public class ChangeScene : MonoBehaviour
{
    public string nextSceneName;

    private Player player;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (nextSceneName == "GoalScene")
        {
            if (player.GetGoalItemFlg())
            {
                FadeManager.Instance.LoadScene(nextSceneName);
            }
        }
        else
        {
            FadeManager.Instance.LoadScene(nextSceneName);
        }
    }
}