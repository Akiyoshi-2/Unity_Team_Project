using UnityEngine;
using System.IO;

public class ItemIconGenerator : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RenderTexture renderTexture;

    [SerializeField] private string fileName = "ItemIcon";

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            GeneratePNG();
        }
    }

    public void GeneratePNG()
    {
        targetCamera.Render();

        RenderTexture currentRT = RenderTexture.active;

        RenderTexture.active = renderTexture;

        Texture2D texture = new Texture2D(
            renderTexture.width,
            renderTexture.height,
            TextureFormat.RGBA32,
            false
        );

        texture.ReadPixels(
            new Rect(
                0,
                0,
                renderTexture.width,
                renderTexture.height
            ),
            0,
            0
        );

        texture.Apply();

        byte[] pngData = texture.EncodeToPNG();

        string path = Path.Combine(
            Application.dataPath,
            fileName + ".png"
        );

        File.WriteAllBytes(path, pngData);

        RenderTexture.active = currentRT;

        Destroy(texture);

        Debug.Log("PNGを保存しました！");
        Debug.Log(path);
    }
}