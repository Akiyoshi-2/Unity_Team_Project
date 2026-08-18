using UnityEngine;

public class ItemSlotManager : MonoBehaviour
{
    private Player player;

    [SerializeField]
    private GameObject slot1ohuda;
    [SerializeField]
    private GameObject slot1Staminam;

    [SerializeField]
    private GameObject slot2ohuda;
    [SerializeField]
    private GameObject slot2Staminam;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
    }

    void Update()
    {
        if (player.GetStockItem1() == 1)
        {
            slot1ohuda.SetActive(true);
        }
        if (player.GetStockItem1() == 2)
        {
            slot1Staminam.SetActive(true);
        }

        if (player.GetStockItem1() == 0)
        {
            slot1ohuda.SetActive(false);
            slot1Staminam.SetActive(false);
        }

        if (player.GetStockItem2() == 1)
        {
            slot2ohuda.SetActive(true);
        }
        if (player.GetStockItem2() == 2)
        {
            slot2Staminam.SetActive(true);
        }

        if (player.GetStockItem2() == 0)
        {
            slot2ohuda.SetActive(false);
            slot2Staminam.SetActive(false);
        }
    }
}
