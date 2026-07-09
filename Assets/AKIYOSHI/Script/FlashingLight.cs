using UnityEngine;

public class FlashingLight : MonoBehaviour
{
    public Transform enemy;
    public Light flashingLight;
    public float flashingDistance = 10f;

    private float timer;
    private float nextBlinkTime;

    private void Start()
    {
        SetNextBlink();
    }

    private void Update()
    {
        if (enemy == null || flashingLight == null) return;

        float distance = Vector3.Distance(transform.position, enemy.position);

        if (distance <= flashingDistance)
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
            flashingLight.enabled = true;
            flashingLight.intensity = 1f;
            timer = 0f;
        }
    }

    private void SetNextBlink()
    {
        nextBlinkTime = Random.Range(0.01f, 0.4f);
    }
}