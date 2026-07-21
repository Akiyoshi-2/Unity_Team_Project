using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Utility;

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
    private CurveControlledBob headBob_ = new CurveControlledBob();

    [SerializeField]
    private Camera m_Camera;

    [SerializeField]
    private GameObject m_Enemy;

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

    private float useItemTimer;

    private bool enemyOutline = false;
    private float outlineTimer = 0;

    private bool staminam = false;
    [SerializeField]
    private float staminamTimer = 0;

    private bool flashLight = false;
    private float flashLightTimer = 0;

    [SerializeField]
    private PostProcessVolume volume = null;
    private Bloom bloom = null;
    private int flashFlg = 0;

    [Header("アイテム設定")]
    [SerializeField]
    private float staminamTime = 10;
    [SerializeField]
    private float ohudaTime = 10;
    [SerializeField]
    private float flashLightTime = 10;

    private void Start()
    {
        volume.profile.TryGetSettings(out bloom);
        headBob_.Setup(m_Camera, 1.0f);
    }

    private void Update()
    {
        Cursor.lockState = CursorLockMode.Locked;

        Vector3 moveXZ = Vector3.zero;

        float moveY = rb.linearVelocity.y;

        if (!tagChange)
        {

            if (Input.GetKey(KeyCode.S))
            {

                Vector3 headBob = headBob_.DoHeadBob(0.8f);
                m_Camera.transform.localPosition = headBob;

                moveXZ += -this.transform.forward * m_BackSpeed;
            }
            if (Input.GetKey(KeyCode.D))
            {

                Vector3 headBob = headBob_.DoHeadBob(0.8f);
                m_Camera.transform.localPosition = headBob;

                moveXZ += this.transform.right * m_SideSpeed;
            }
            if (Input.GetKey(KeyCode.A))
            {

                Vector3 headBob = headBob_.DoHeadBob(0.8f);
                m_Camera.transform.localPosition = headBob;

                moveXZ += -this.transform.right * m_SideSpeed;
            }

            if (Input.GetKey(KeyCode.W))
            {

                Vector3 headBob = headBob_.DoHeadBob(0.8f);
                m_Camera.transform.localPosition = headBob;

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

                    if (raycastHit.collider.name == "お札")
                    {
                        getItemID = 1;
                    }
                    if (raycastHit.collider.name == "スタミナム")
                    {
                        getItemID = 2;
                    }
                    if (raycastHit.collider.name == "フラッシュライト")
                    {
                        getItemID = 3;
                    }

                    if (item1Stock == 0)
                    {
                        item1Stock = getItemID;
                        Debug.Log(GetItemName(item1Stock) + "を拾った"); //消す
                    }
                    else if (item2Stock == 0)
                    {
                        item2Stock = getItemID;
                        Debug.Log(GetItemName(item2Stock) + "を拾った"); //消す
                    }
                }
                else if (hit && raycastHit.collider.tag == "item" && item1Stock != 0 && item2Stock != 0)
                {
                    Debug.Log("これ以上アイテムを拾えません"); //消す
                }

                if (hit && raycastHit.collider.tag == "HideBox")
                {
                    posSave = this.transform.position;
                    this.transform.localScale = Vector3.zero;
                    this.transform.position = raycastHit.transform.position;
                    this.transform.localEulerAngles =
                        new Vector3
                        (raycastHit.transform.localEulerAngles.x,
                        raycastHit.transform.localEulerAngles.y + 90,
                        raycastHit.transform.localEulerAngles.z)
                        ;

                    dirStop = raycastHit.transform.localEulerAngles.y + 90;
                    rb.useGravity = false;
                    tagChange = true;
                }

                if (hit && raycastHit.collider.tag == "Door")
                {
                    //raycastHit.transform.localEulerAngles.y = 0;
                }
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (item1Stock != 0 && useItemTimer <= 0)
                {
                    Debug.Log(GetItemName(item1Stock) + "を使った"); //消す
                    Item(item1Stock);
                    useItemTimer = 1.5f;
                    item1Stock = 0;
                }
                else if (item1Stock != 0 && useItemTimer >= 0)
                {
                    Debug.Log("アイテムクールタイム中,　残り時間(" + useItemTimer + "秒)"); //消す
                }

            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                if (item2Stock != 0 && useItemTimer <= 0)
                {
                    Debug.Log(GetItemName(item2Stock) + "を使った"); //消す
                    Item(item2Stock);
                    useItemTimer = 1.5f;
                    item2Stock = 0;
                }
                else if (item2Stock != 0 && useItemTimer >= 0)
                {
                    Debug.Log("アイテムクールタイム中・残り時間(" + useItemTimer + "秒)"); //消す
                }
            }
            useItemTimer -= Time.deltaTime;
            if (useItemTimer < 0)
            {
                useItemTimer = 0;
            }

            // 後で消す
            if (Input.GetKeyDown(KeyCode.I))
            {
                Debug.Log("Item1 : " + GetItemName(item1Stock) + "  Item2 : " + GetItemName(item2Stock));
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
            m_Enemy.GetComponent<Outline>().enabled = true;
            outlineTimer += Time.deltaTime;

            if (outlineTimer > ohudaTime)
            {
                enemyOutline = false;
            }
        }
        else
        {
            m_Enemy.GetComponent<Outline>().enabled = false;
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

            if (staminamTimer > staminamTime)
            {
                staminam = false;
            }
        }
        else
        {
            staminamTimer = 0;
        }

        if (flashLight)
        {
            flashLightTimer += Time.deltaTime;
            float distance = Vector3.Distance(this.transform.position, m_Enemy.transform.position);
            if (distance <= 20.0f)
            {
                //m_Enemy.GetComponent<Enemy>().flashLightHit = true;
            }

            if (flashFlg == 0)
            {
                bloom.enabled.value = true;
                bloom.intensity.value += Time.deltaTime * 100;
                if (bloom.intensity.value > 50)
                {
                    bloom.intensity.value = 50;
                    flashFlg = 1;
                }
            }
            if (flashFlg == 1)
            {
                bloom.intensity.value -= Time.deltaTime * 100;
                if (bloom.intensity.value < 0)
                {
                    bloom.intensity.value = 0;
                    bloom.enabled.value = false;
                    flashFlg = 2;
                }
            }
            if (flashFlg == 2 /*&& !m_Enemy.GetComponent<Enemy>().flashLightHit*/)
            {
                flashLight = false;
            }

            if (flashLightTimer > flashLightTime)
            {
                //m_Enemy.GetComponent<Enemy>().flashLightHit = false;
                flashLight = false;
            }
        }
        else
        {
            flashFlg = 0;
            flashLightTimer = 0;
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
        if (i == 1 && !enemyOutline)
        {
            enemyOutline = true;
        }
        else if (i == 1 && enemyOutline)
        {
            outlineTimer = 0;
        }

        if (i == 2 && !staminam)
        {
            staminam = true;
        }
        else if (i == 2 && staminam)
        {
            staminamTimer = 0;
        }

        if (i == 3 && !flashLight)
        {
            flashLight = true;
        }
        else if (i == 3 && flashLight)
        {
            flashFlg = 0;
            flashLightTimer = 0;
        }
    }

    private System.String GetItemName(int ID)
    {
        if (ID == 1)
        {
            return "お札";
        }
        if (ID == 2)
        {
            return "スタミナム";
        }
        if (ID == 3)
        {
            return "フラッシュライト";
        }
        else
        {
            return null;
        }
    }
}
