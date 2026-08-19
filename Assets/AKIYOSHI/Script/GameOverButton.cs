using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverButton : MonoBehaviour
{
    // ゲームをやりなおす
    public void Retry()
    {
        SceneManager.LoadScene("SelectScene");
    }

    // タイトルに戻る
    public void BackToTitle()
    {
        SceneManager.LoadScene("Kawakami");
    }
}