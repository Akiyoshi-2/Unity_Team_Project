using UnityEngine;

public class FlashingLight : MonoBehaviour
{
    public Light flashingLight;
    public float flashingDistance = 10.0f;

    private float timer;
    private float nextBlinkTime;

    void Start()
    {
        SetNextBlink();
    }

    void Update()
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
            flashingLight.intensity = 1f;
            return;
        }

        // àÍî‘ãﬂÇ¢ìGÇ™ãﬂÇ¢èÍçá
        if (nearestDistance <= flashingDistance)
        {
            timer += Time.deltaTime;

            if (timer >= nextBlinkTime)
            {
                flashingLight.enabled = !flashingLight.enabled;
                flashingLight.intensity = Random.Range(0.5f, 1.5f);

                timer = 0f;
                SetNextBlink();
            }
        }
        else
        {
            // ìGÇ™âìÇ¢èÍçá
            flashingLight.enabled = true;
            flashingLight.intensity = 1f;
            timer = 0f;
        }
    }

    void SetNextBlink()
    {
        nextBlinkTime = Random.Range(0.01f, 0.4f);
    }
}