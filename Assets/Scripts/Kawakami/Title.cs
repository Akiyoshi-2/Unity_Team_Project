using UnityEngine;
using UnityEngine.SceneManagement;

public class Title : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SceneManager.LoadScene("SelectScene");
        }
    }


}
