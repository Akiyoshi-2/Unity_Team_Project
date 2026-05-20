using System;
using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    [SerializeField]
    private Image redGauge;

    [SerializeField]
    private Image greenGauge;

    [SerializeField]
    private Player player;

    void Update()
    {
        if (player.staminaOutbool())
        {
            redGauge.fillAmount = player.staminaNum() / 10f * 0.75f;
            greenGauge.fillAmount = 0.0f;
        }
        else
        {
            greenGauge.fillAmount = player.staminaNum() / 10f * 0.75f;
            redGauge.fillAmount = 0.0f;
        }
    }
}
