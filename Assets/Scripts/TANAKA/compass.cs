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

    private Vector3 targetPos;

    private Transform m_Player;

    private void Start()
    {
        Transform player = GameObject.FindGameObjectWithTag("Player").transform;
        if (player != null) m_Player = player;
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

        Vector3 dir = targetPos - m_Player.position;

        dir.y = 0;

        float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        float angle = targetAngle - m_Player.localEulerAngles.y;

        this.transform.localRotation = Quaternion.Euler(0, 0, -angle);
    }
}
