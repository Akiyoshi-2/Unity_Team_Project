using UnityEngine;

public class GOVManager : MonoBehaviour
{
    private void Start()
    {
        LockCursor();
    }

    // カーソルをロック
    public void LockCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // カーソルのロックを解除
    public void UnlockCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}