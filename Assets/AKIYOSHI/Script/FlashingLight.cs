using System;
using UnityEngine;

public class FlashingLight : MonoBehaviour
{
    public Light flashingLight;
    public float flashingDistance = 10.0f;

    private float timer;
    private float nextBlinkTime;

    [NonSerialized]
    public bool LightOn = false;

    void Start()
    {
        SetNextBlink();
    }

    void Update()
    {
        if (LightOn)
        {

            if (flashingLight == null)
                return;

            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

            float nearestDistance = Mathf.Infinity;

            foreach (GameObject enemy in enemies)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                }
            }

            // ìGÇ™1ëÃÇ‡Ç¢Ç»Ç¢èÍçá
            if (nearestDistance == Mathf.Infinity)
            {
                flashingLight.enabled = true;
                flashingLight.intensity = 2f;
                return;
            }

            // àÍî‘ãﬂÇ¢ìGÇ™ãﬂÇ¢èÍçá
            if (nearestDistance <= flashingDistance)
            {
                timer += Time.deltaTime;

                if (timer >= nextBlinkTime)
                {
                    flashingLight.enabled = !flashingLight.enabled;
                    flashingLight.intensity = UnityEngine.Random.Range(0.5f, 2.5f);

                    timer = 0f;
                    SetNextBlink();
                }
            }
            else
            {
                // ìGÇ™âìÇ¢èÍçá
                flashingLight.enabled = true;
                flashingLight.intensity = 2f;
                timer = 0f;
            }

        }
        else
        {
            flashingLight.enabled = false;
        }
    }

    void SetNextBlink()
    {
        nextBlinkTime = UnityEngine.Random.Range(0.01f, 0.4f);
    }
}