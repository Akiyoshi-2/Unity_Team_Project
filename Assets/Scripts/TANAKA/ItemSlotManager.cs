using UnityEngine;
using UnityEngine.UI;

public class ItemSlotManager : MonoBehaviour
{
    private Player player;

    [SerializeField]
    private GameObject slot1ohuda;
    [SerializeField]
    private GameObject slot1Staminam;
    [SerializeField]
    private GameObject slot1flashLight;
    [SerializeField]
    private GameObject slot1Clock;
    [SerializeField]
    private GameObject slot1LightStone;
    [SerializeField]
    private Text slot1count;

    [SerializeField]
    private GameObject slot2ohuda;
    [SerializeField]
    private GameObject slot2Staminam;
    [SerializeField]
    private GameObject slot2flashLight;
    [SerializeField]
    private GameObject slot2Clock;
    [SerializeField]
    private GameObject slot2LightStone;
    [SerializeField]
    private Text slot2count;

    [SerializeField]
    private GameObject slot3ohuda;
    [SerializeField]
    private GameObject slot3Staminam;
    [SerializeField]
    private GameObject slot3flashLight;
    [SerializeField]
    private GameObject slot3Clock;
    [SerializeField]
    private GameObject slot3LightStone;
    [SerializeField]
    private Text slot3count;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
    }

    void Update()
    {
        if (player.GetStockItem1() != 0)
        {
            if (player.GetStockItem1() == 1)
            {
                slot1ohuda.SetActive(true);
            }
            if (player.GetStockItem1() == 2)
            {
                slot1Staminam.SetActive(true);
            }
            if (player.GetStockItem1() == 3)
            {
                slot1flashLight.SetActive(true);
            }
            if (player.GetStockItem1() == 4)
            {
                slot1Clock.SetActive(true);
            }
            if (player.GetStockItem1() == 5)
            {
                slot1LightStone.SetActive(true);
            }

            slot1count.gameObject.SetActive(true);
            slot1count.text =  ("" + player.GetItem1Count());
        }
        else if (player.GetStockItem1() == 0)
        {
            slot1ohuda.SetActive(false);
            slot1Staminam.SetActive(false);
            slot1flashLight.SetActive(false);
            slot1Clock.SetActive(false);
            slot1LightStone.SetActive(false);
            slot1count.gameObject.SetActive(false);
        }

        if (player.GetStockItem2() != 0)
        {
            if (player.GetStockItem2() == 1)
            {
                slot2ohuda.SetActive(true);
            }
            if (player.GetStockItem2() == 2)
            {
                slot2Staminam.SetActive(true);
            }
            if (player.GetStockItem2() == 3)
            {
                slot2flashLight.SetActive(true);
            }
            if (player.GetStockItem2() == 4)
            {
                slot2Clock.SetActive(true);
            }
            if (player.GetStockItem2() == 5)
            {
                slot2LightStone.SetActive(true);
            }

            slot2count.gameObject.SetActive(true);
            slot2count.text = ("" + player.GetItem2Count());
        }
        else if (player.GetStockItem2() == 0)
        {
            slot2ohuda.SetActive(false);
            slot2Staminam.SetActive(false);
            slot2flashLight.SetActive(false);
            slot2Clock.SetActive(false);
            slot2LightStone.SetActive(false);
            slot2count.gameObject.SetActive(false);
        }

        if (player.GetStockItem3() != 0)
        {
            if (player.GetStockItem3() == 1)
            {
                slot3ohuda.SetActive(true);
            }
            if (player.GetStockItem3() == 2)
            {
                slot3Staminam.SetActive(true);
            }
            if (player.GetStockItem3() == 3)
            {
                slot3flashLight.SetActive(true);
            }
            if (player.GetStockItem3() == 4)
            {
                slot3Clock.SetActive(true);
            }
            if (player.GetStockItem3() == 5)
            {
                slot3LightStone.SetActive(true);
            }

            slot3count.gameObject.SetActive(true);
            slot3count.text = ("" + player.GetItem3Count());
        }
        else if (player.GetStockItem3() == 0)
        {
            slot3ohuda.SetActive(false);
            slot3Staminam.SetActive(false);
            slot3flashLight.SetActive(false);
            slot3Clock.SetActive(false);
            slot3LightStone.SetActive(false);
            slot3count.gameObject.SetActive(false);
        }
    }
}
