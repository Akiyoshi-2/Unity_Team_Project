using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public static Player Instance;

    private Rigidbody _rb;
    public Rigidbody rb
    {
        get
        {
            if (_rb == null)
            {
                _rb = this.GetComponent<Rigidbody>();
            }
            return _rb;
        }
    }

    [SerializeField]
    private Camera m_Camera;

    [SerializeField]
    private Outline m_Enemy;

    Vector3 camera_m;
    Vector3 player_m;

    private float m_ForwardSpeed = 6f;
    private float m_BackSpeed = 3f;
    private float m_SideSpeed = 3f;
    private float m_RotationSpeed = 2f;

    private int playerMoveFlg = 0;

    private bool Squat = false;
    private bool SquatMove = false;

    [SerializeField]
    private float staminaTime = 4.0f;
    [SerializeField]
    private float staminaHealTime = 3.0f;

    private float stamina = 10.0f;
    private bool staminaOut = false;
    private bool run = false;
    private bool walk = false;

    private bool tagChange = false;

    private Vector3 posSave = Vector3.zero;
    private float dirStop;

    private int getItemID = 0;

    private int item1Stock = 0;
    private int item2Stock = 0;

    private bool enemyOutline = false;
    private float outlineTimer = 0;

    private bool staminam = false;
    private float staminamTimer = 0;

    private void Update()
    {
        Cursor.lockState = CursorLockMode.Locked;

        Vector3 moveXZ = Vector3.zero;

        float moveY = rb.linearVelocity.y;

        if (!tagChange)
        {

            if (Input.GetKey(KeyCode.S))
            {
                moveXZ += -this.transform.forward * m_BackSpeed;
            }
            if (Input.GetKey(KeyCode.D))
            {
                moveXZ += this.transform.right * m_SideSpeed;
            }
            if (Input.GetKey(KeyCode.A))
            {
                moveXZ += -this.transform.right * m_SideSpeed;
            }

            if (Input.GetKey(KeyCode.W))
            {
                moveXZ += this.transform.forward * m_ForwardSpeed;
                if (run && !staminaOut && !staminam)
                {
                    stamina -= Time.deltaTime * 10 / staminaTime;
                    if (stamina <= 0)
                    {
                        run = false;
                        staminaOut = true;
                    }
                }
                walk = true;
            }
            else
            {
                walk = false;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {

                RaycastHit raycastHit;

                bool hit = Physics.Raycast(m_Camera.transform.position, m_Camera.transform.forward, out raycastHit, 2.5f);
                if (hit && raycastHit.collider.tag == "item" && (item1Stock == 0 || item2Stock == 0))
                {
                    raycastHit.collider.gameObject.SetActive(false);
                    Debug.Log(raycastHit.collider.gameObject.name + "を拾った"); //後で消す

                    if (raycastHit.collider.name == "お札")
                    {
                        getItemID = 1;
                    }
                    if (raycastHit.collider.name == "スタミナム")
                    {
                        getItemID = 2;
                    }

                    if (item1Stock == 0)
                    {
                        item1Stock = getItemID;
                    }
                    else if (item2Stock == 0)
                    {
                        item2Stock = getItemID;
                    }
                }
                else if (hit && raycastHit.collider.tag == "item" && item1Stock != 0 && item2Stock != 0)
                {
                    Debug.Log("これ以上アイテムを拾えません"); //後で消す
                }

                if (hit && raycastHit.collider.tag == "HideBox")
                {
                    posSave = this.transform.position;
                    this.transform.localScale = Vector3.zero;
                    this.transform.position = raycastHit.transform.position;
                    this.transform.localEulerAngles = raycastHit.transform.localEulerAngles;
                    dirStop = raycastHit.transform.localEulerAngles.y;
                    rb.useGravity = false;
                    tagChange = true;
                }
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (item1Stock != 0)
                {
                    Item(item1Stock);
                    item1Stock = 0;
                }

            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                if (item2Stock != 0)
                {
                    Item(item2Stock);
                    item2Stock = 0;
                }
            }

            // 後で消す
            if (Input.GetKeyDown(KeyCode.I))
            {
                Debug.Log("Item1 : " + item1Stock + "  Item2 : " + item2Stock);
            }

            if (walk && Input.GetKey(KeyCode.LeftShift) && !Squat && !staminaOut)
            {
                m_ForwardSpeed = 10;
                m_SideSpeed = 3;
                m_BackSpeed = 3;
                run = true;
            }
            else if (!Squat && !staminaOut)
            {
                m_ForwardSpeed = 6;
                m_SideSpeed = 3;
                m_BackSpeed = 3;
                run = false;
            }
            else
            {
                run = false;
            }

            if (Input.GetKeyDown(KeyCode.C) && !staminaOut)
            {
                if (!Squat)
                {
                    this.transform.position = new Vector3(
                    this.transform.position.x,
                    this.transform.position.y - 0.5f,
                    this.transform.position.z);
                }
                Squat = !Squat;
                SquatMove = true;
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.F))
            {

                if (Squat)
                {
                    this.transform.localScale = new Vector3(1.0f, 0.5f, 1.0f);
                }
                else
                {
                    this.transform.localScale = Vector3.one;
                }
                this.transform.position = posSave;
                rb.useGravity = true;
                tagChange = false;
            }
        }

        if (enemyOutline)
        {
            m_Enemy.enabled = true;
            outlineTimer += Time.deltaTime;

            if (outlineTimer > 10)
            {
                enemyOutline = false;
            }
        }
        else
        {
            m_Enemy.enabled = false;
            outlineTimer = 0;
        }

        if (staminam)
        {
            staminamTimer += Time.deltaTime;
            staminaOut = false;

            stamina += Time.deltaTime * 10;

            if (stamina >= 10.0f)
            {
                stamina = 10.0f;
            }

            if (staminamTimer > 10)
            {
                staminam = false;
            }
        }
        else
        {
            staminamTimer = 0;
        }

        if ((staminaOut || tagChange) && !staminam)
        {
            m_ForwardSpeed = 2f;
            m_BackSpeed = 2f;
            m_SideSpeed = 2f;
            stamina += Time.deltaTime * 10 / staminaHealTime;
            if (stamina >= 10.0f)
            {
                staminaOut = false;
            }
            if (stamina >= 10.0f)
            {
                stamina = 10.0f;
            }
        }

        if (!staminaOut && !run && stamina <= 10.0f && !staminam)
        {
            stamina += Time.deltaTime * 10 / staminaHealTime;
        }

        if (tagChange)
        {
            this.transform.tag = "Invisible";
        }
        else
        {
            this.transform.tag = "Player";
        }

        rb.linearVelocity = new Vector3(moveXZ.x, moveY, moveXZ.z);

        if (!tagChange)
        {
            player_m = new Vector3(
                0,
                this.transform.localEulerAngles.y + Input.GetAxis("Mouse X") * m_RotationSpeed,
                0);

            camera_m = new Vector3(
                Mathf.Clamp(camera_m.x - Input.GetAxis("Mouse Y") * m_RotationSpeed, -80f, 80f),
                m_Camera.transform.localEulerAngles.y,
                0f);
        }
        else
        {
            player_m = new Vector3(
                0,
                Mathf.Clamp(player_m.y + Input.GetAxis("Mouse X") * m_RotationSpeed, dirStop - 30, dirStop + 30),
                0f);

            camera_m = new Vector3(
               Mathf.Clamp(camera_m.x - Input.GetAxis("Mouse Y") * m_RotationSpeed, -30f, 20f),
              m_Camera.transform.localEulerAngles.y,
               0f);
        }

        this.transform.localEulerAngles = player_m;
        m_Camera.transform.localEulerAngles = camera_m;

        if (Squat && SquatMove)
        {
            this.transform.localScale = new Vector3(
                this.transform.localScale.x,
                0.5f,
                this.transform.localScale.z);
            m_ForwardSpeed = 2f;
            m_BackSpeed = 2f;
            m_SideSpeed = 2f;
            SquatMove = false;
        }


        if (!Squat && SquatMove)
        {
            RaycastHit hit;
            if (Physics.Raycast(
              new Vector3(
                  this.transform.position.x + 0.15f,
                  this.transform.position.y,
                  this.transform.position.z),
              this.transform.up, out hit, 1.2f) ||
              Physics.Raycast(
              new Vector3(
                  this.transform.position.x,
                  this.transform.position.y + 0.15f,
                  this.transform.position.z),
              this.transform.up, out hit, 1.2f) ||
              Physics.Raycast(
              new Vector3(
                  this.transform.position.x - 0.15f,
                  this.transform.position.y,
                  this.transform.position.z),
              this.transform.up, out hit, 1.2f) ||
              Physics.Raycast(
              new Vector3(
                  this.transform.position.x,
                  this.transform.position.y - 0.15f,
                  this.transform.position.z),
              this.transform.up, out hit, 1.2f))
            {
                Squat = true;
                SquatMove = false;
                return;
            }
            this.transform.localScale = new Vector3(
            this.transform.localScale.x,
            1,
            this.transform.localScale.z);
            m_ForwardSpeed = 6;
            m_SideSpeed = 5;
            m_BackSpeed = 5;
            SquatMove = false;
        }
    }

    public float staminaNum()
    {
        return stamina;
    }

    public bool staminaOutbool()
    {
        return staminaOut;
    }

    private void Item(int i)
    {
        if (i == 1)
        {
            enemyOutline = true;
        }
        
        if (i == 2)
        {
            staminam = true;
        }
    }
}
