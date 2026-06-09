using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

    Vector3 camera_m;

    private float m_ForwardSpeed = 6f;
    private float m_BackSpeed = 3f;
    private float m_SideSpeed = 3f;
    private float m_RotationSpeed = 2f;

    private bool Squat = false;
    private bool SquatMove = false;

    private float stamina = 10.0f;
    private bool staminaOut = false;
    private bool run = false;
    private bool walk = false;

    private bool tagChange = false;

    [SerializeField]
    private float staminaTime = 4.0f;
    [SerializeField]
    private float staminaHealTime = 3.0f;

    private void Update()
    {
        Cursor.lockState = CursorLockMode.Locked;

        Vector3 moveXZ = Vector3.zero;

        float moveY = rb.linearVelocity.y;

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
            if (run && !staminaOut)
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

        if (staminaOut)
        {
            m_ForwardSpeed = 2f;
            m_BackSpeed = 2f;
            m_SideSpeed = 2f;
            stamina += Time.deltaTime * 10 / staminaHealTime;
            if (stamina >= 10.0f)
            {
                staminaOut = false;
            }
        }

        if (!staminaOut && !run && stamina <= 10.0f)
        {
            stamina += Time.deltaTime * 10 / staminaHealTime;
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

        if (Input.GetKeyDown(KeyCode.T))
        {
            tagChange = !tagChange;
        }

        if (tagChange)
        {
            this.transform.tag = "Invisible";
        }
        else
        {
            this.transform.tag = "Player";
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            RaycastHit raycastHit;

            bool hit = Physics.Raycast(m_Camera.transform.position, m_Camera.transform.forward, out raycastHit, 2.5f);
            if (hit && raycastHit.collider.tag == "item")
            {
                raycastHit.collider.gameObject.SetActive(false);
                Debug.Log(raycastHit.collider.gameObject.name + "‚ðE‚Á‚½"); //Œã‚ÅÁ‚·
            }
        }

        rb.linearVelocity = new Vector3(moveXZ.x, moveY, moveXZ.z);

        this.transform.localEulerAngles = new Vector3(0, this.transform.localEulerAngles.y + Input.GetAxis("Mouse X") * m_RotationSpeed, 0);

        camera_m = new Vector3(
            Mathf.Clamp(camera_m.x - Input.GetAxis("Mouse Y") * m_RotationSpeed, -80f, 80f),
            m_Camera.transform.localEulerAngles.y,
            0f);
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
}
