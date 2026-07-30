using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    public string nextSceneName; // à⁄ìÆêÊÇÃÉVÅ[Éìñº

    private Player player;
    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (nextSceneName == "GoalScene")
            {
                if (player.GetGoalItemFlg())
                {
                    SceneManager.LoadScene(nextSceneName);
                }
            }
            else
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }
    }
}
