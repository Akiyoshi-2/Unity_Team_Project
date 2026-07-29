using Unity.VisualScripting;
using UnityEngine;

public class compass : MonoBehaviour
{
    public enum PositionType
    {
        Position,
        Object
    }

    [SerializeField]
    private PositionType positionType;

    [SerializeField]
    private Vector3 targetPosition;

    [SerializeField]
    private GameObject targetObject;

    private GameObject player;

    private Vector3 targetPos;

    private Transform transformPlayer;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) transformPlayer = player.transform;
    }

    void Update()
    {
        if (positionType == PositionType.Position)
        {
            targetPos = targetPosition;
        }
        else if (positionType == PositionType.Object)
        {
            targetPos = targetObject.transform.position;
        }

        Vector3 dir = targetPos - transformPlayer.position;

        dir.y = 0;

        float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        float angle = targetAngle - transformPlayer.localEulerAngles.y;

        this.transform.localRotation = Quaternion.Euler(0, 0, -angle);
    }
}
