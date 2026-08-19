using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private GameOverButton gameOverButton;
    [SerializeField] private GOVManager govManager;

    private bool isMenuOpen = false;

    private void Start()
    {
        menuCanvas.SetActive(false);

        // ゲーム開始時はカーソルをロック
        govManager.LockCursor();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMenu();
        }
    }

    private void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        menuCanvas.SetActive(isMenuOpen);

        if (isMenuOpen)
        {
            // ゲームを停止
            Time.timeScale = 0f;

            // カーソルを表示・ロック解除
            govManager.UnlockCursor();
        }
        else
        {
            // ゲームを再開
            Time.timeScale = 1f;

            // カーソルを非表示・ロック
            govManager.LockCursor();
        }
    }

    // ゲームに戻る
    public void Resume()
    {
        isMenuOpen = false;

        menuCanvas.SetActive(false);

        Time.timeScale = 1f;

        // カーソルをロック
        govManager.LockCursor();
    }

    // ステージセレクトへ
    public void GoToStageSelect()
    {
        Time.timeScale = 1f;

        gameOverButton.Retry();
    }

    // タイトルへ
    public void GoToTitle()
    {
        Time.timeScale = 1f;

        gameOverButton.BackToTitle();
    }
}