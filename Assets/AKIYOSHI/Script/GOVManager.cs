using UnityEngine;

public class GOVManager : MonoBehaviour
{
    private void Start()
    {
        // マウスカーソルを表示
        Cursor.visible = true;

        // マウスカーソルのロックを解除
        Cursor.lockState = CursorLockMode.None;
    }
}
