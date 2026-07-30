using UnityEngine;

public class GetCompass : MonoBehaviour
{
    private Player player;

    [SerializeField]
    private GameObject Compass;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
    }

    void Update()
    {
        if (player.GetCompassFlg())
        {
            if (!Compass.gameObject.activeSelf)
            {
                Compass.gameObject.SetActive(true);
            }

        }
        else
        {
            if (Compass.gameObject.activeSelf)
            {
                Compass.gameObject.SetActive(false);
            }
        }
    }
}
