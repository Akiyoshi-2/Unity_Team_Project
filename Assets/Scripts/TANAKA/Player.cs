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
    private GameObject playerLight;

    private bool playerLightOnOff = false;

    [SerializeField]
    private Camera m_Camera;

    private Enemy[] m_Enemies;

    Vector3 camera_m;
    Vector3 player_m;

    [Header("プレイヤー設定")]
    [SerializeField] private float WalkSpeed = 4f;
    [SerializeField] private float DashSpeed = 10f;
    [SerializeField] private float BackSideSpeed = 3f;
    [SerializeField] private float SquatSpeed = 2f;

    private float m_ForwardSpeed = 4f;
    private float m_BackSpeed = 3f;
    private float m_SideSpeed = 3f;

    [SerializeField]
    private float m_RotationSpeed = 2f;

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

    // 足音設定
    [SerializeField]
    private float walkStepInterval = 0.5f;

    [SerializeField]
    private float runStepInterval = 0.3f;

    [SerializeField]
    private float squatStepInterval = 0.7f;

    private float footstepTimer = 0f;

    private bool tagChange = false;

    private Vector3 posSave = Vector3.zero;
    private float dirStop;

    private int getItemID = 0;

    private int item1Stock = 0;
    private int item2Stock = 0;
    private int item3Stock = 0;

    private int getItemUseCount = 0;

    private int item1UseCount = 0;
    private int item2UseCount = 0;
    private int item3UseCount = 0;

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

    [SerializeField]
    private GameObject flashStone;

    // アイテム使ったらtrue
    [NonSerialized]
    public bool clockFlg = false;
    
    private Vector3 clockPos = Vector3.zero;

    private bool getCompass = false;

    private int doorFlg = 0;
    private Transform door = null;

    private Transform Light = null;

    private bool GoalItemFlg = false;

    private SoundManager sound;

    [Header("足音")]
    [SerializeField]
    private AudioClip walkClip;

    [SerializeField]
    private AudioClip runClip;

    private AudioSource footstepSource;

    private bool flashEnemyChecked = false;

    GameObject[] enemyObjects = null;

    private void Start()
    {
        volume.profile.TryGetSettings(out bloom);
        headBob_.Setup(m_Camera, 1.0f);

        sound = GameObject.FindGameObjectWithTag("SoundManager")
                         .GetComponent<SoundManager>();

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.loop = true;
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 1f;

        // シーン内のEnemyをすべて取得
        enemyObjects = GameObject.FindGameObjectsWithTag("Enemy");

        m_Enemies = new Enemy[enemyObjects.Length];

        for (int i = 0; i < enemyObjects.Length; i++)
        {
            m_Enemies[i] = enemyObjects[i].GetComponent<Enemy>();
        }
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
            }

            if (Input.GetKeyDown(KeyCode.F))
            {

                RaycastHit raycastHit;

                bool hit = Physics.Raycast(m_Camera.transform.position, m_Camera.transform.forward, out raycastHit, 2.5f);
                if (hit && raycastHit.collider.tag == "item" && (item1Stock == 0 || item2Stock == 0 || item3Stock == 0))
                {
                    raycastHit.collider.gameObject.SetActive(false);

                    if (raycastHit.collider.name == "御札" || raycastHit.collider.name == "御札(Clone)")
                    {
                        getItemID = 1;
                        getItemUseCount = 1;
                    }
                    if (raycastHit.collider.name == "スタミナム" || raycastHit.collider.name == "スタミナム(Clone)")
                    {
                        getItemID = 2;
                        getItemUseCount = 1;
                    }
                    if (raycastHit.collider.name == "フラッシュライト" || raycastHit.collider.name == "フラッシュライト(Clone)")
                    {
                        getItemID = 3;
                        getItemUseCount = 1;
                    }
                    if (raycastHit.collider.name == "時計" || raycastHit.collider.name == "時計(Clone)")
                    {
                        getItemID = 4;
                        getItemUseCount = 1;
                    }
                    if (raycastHit.collider.name == "光石" || raycastHit.collider.name == "光石(Clone)")
                    {
                        getItemID = 5;
                        getItemUseCount = 5;
                    }

                    if (item1Stock == 0)
                    {
                        item1Stock = getItemID;
                        item1UseCount = getItemUseCount;
                        Debug.Log(GetItemName(item1Stock) + "を拾った"); //消す
                        getItemID = 0;
                        getItemUseCount = 0;
                    }
                    else if (item2Stock == 0)
                    {
                        item2Stock = getItemID;
                        item2UseCount = getItemUseCount;
                        Debug.Log(GetItemName(item2Stock) + "を拾った"); //消す
                        getItemID = 0;
                        getItemUseCount = 0;
                    }
                    else if (item3Stock == 0)
                    {
                        item3Stock = getItemID;
                        item3UseCount = getItemUseCount;
                        Debug.Log(GetItemName(item3Stock) + "を拾った"); //消す
                        getItemID = 0;
                        getItemUseCount = 0;
                    }

                }
                else if (hit && raycastHit.collider.tag == "item" && item1Stock != 0 && item2Stock != 0 && item3Stock != 0)
                {
                    Debug.Log("これ以上アイテムを拾えません"); //消す
                }

                if (hit && raycastHit.collider.tag == "item" && (raycastHit.collider.name == "コンパス" || raycastHit.collider.name == "コンパス(Clone)"))
                {
                    getCompass = true;
                    raycastHit.collider.gameObject.SetActive(false);
                    Debug.Log( "コンパスを拾った"); //消す
                }

                if (hit && raycastHit.collider.tag == "GoalItem")
                {
                    GoalItemFlg = true;
                    raycastHit.collider.gameObject.SetActive(false);
                }

                if (hit && raycastHit.collider.CompareTag("HideBox"))
                {
                    posSave = transform.position;

                    transform.localScale = Vector3.zero;

                    this.GetComponent<CapsuleCollider>().isTrigger = true;

                    // HideBoxの中心よりY方向に1m上へ
                    transform.position = raycastHit.transform.position + Vector3.up * 0.53f + Vector3.left * 0.15f;

                    dirStop = raycastHit.transform.localEulerAngles.y + 90;

                    rb.useGravity = false;
                    tagChange = true;

                    m_Camera.transform.localEulerAngles += new Vector3(0f, 180f, 0f);

                    m_Camera.fieldOfView = 30f;
                }

                if (hit && raycastHit.collider.tag == "Door" && doorFlg == 0)
                {
                    if (raycastHit.transform.localEulerAngles.y == 0)
                    {
                        door = raycastHit.transform;
                        doorFlg = 1;
                    }
                    else if (raycastHit.transform.localEulerAngles.y == 110)
                    {
                        door = raycastHit.transform;
                        doorFlg = 2;
                    }
                }

                if (hit && raycastHit.collider.tag == "wDoor" && doorFlg == 0)
                {
                    if (raycastHit.transform.localPosition.z == 0.75)
                    {
                        door = raycastHit.transform;
                        doorFlg = 3;
                    }
                    else if (raycastHit.transform.localPosition.z == -0.75)
                    {
                        door = raycastHit.transform;
                        doorFlg = 4;
                    }
                }
                if (hit && raycastHit.collider.tag == "Light")
                {
                    Light =  raycastHit.transform;
                    Light.GetComponent<FlashingLight>().LightOn = true;
                    Light.GetComponent<Light>().enabled = true;
                }
            }

            if (doorFlg == 1)
            {
                door.GetComponent<Collider>().isTrigger = true;
                door.transform.localEulerAngles = new Vector3(
                    door.localEulerAngles.x,
                    door.localEulerAngles.y + (220 * Time.deltaTime),
                    door.localEulerAngles.z);

                if (door.localEulerAngles.y >= 110)
                {
                    door.transform.localEulerAngles = new Vector3(
                    door.localEulerAngles.x,
                    110,
                    door.localEulerAngles.z);
                    door.GetComponent<Collider>().isTrigger = false;
                    door = null;
                    doorFlg = 0;
                }
            }

            if (doorFlg == 2)
            {
                door.GetComponent<Collider>().isTrigger = true;
                door.transform.localEulerAngles = new Vector3(
                    door.localEulerAngles.x,
                    door.localEulerAngles.y - (220 * Time.deltaTime),
                    door.localEulerAngles.z);

                if (door.localEulerAngles.y <= 0 || door.localEulerAngles.y >= 150)
                {
                    door.transform.localEulerAngles = new Vector3(
                    door.localEulerAngles.x,
                    0,
                    door.localEulerAngles.z);
                    door.GetComponent<Collider>().isTrigger = false;
                    door = null;
                    doorFlg = 0;
                }
            }

            if (doorFlg == 3)
            {
                door.localPosition = new Vector3(
                    door.localPosition.x,
                    door.localPosition.y,
                    door.localPosition.z - (3.0f * Time.deltaTime));

                if (door.localPosition.z <= -0.75)
                {
                    door.localPosition = new Vector3(
                    door.localPosition.x,
                    door.localPosition.y,
                    -0.75f);
                    door = null;
                    doorFlg = 0;
                }
            }

            if (doorFlg == 4)
            {
                door.localPosition = new Vector3(
                    door.localPosition.x,
                    door.localPosition.y,
                    door.localPosition.z + (3.0f * Time.deltaTime));

                if (door.localPosition.z >= 0.75)
                {
                    door.localPosition = new Vector3(
                    door.localPosition.x,
                    door.localPosition.y,
                    0.75f);
                    door = null;
                    doorFlg = 0;
                }
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (item1Stock != 0 && useItemTimer <= 0)
                {
                    Debug.Log(GetItemName(item1Stock) + "を使った"); //消す
                    Item(item1Stock);
                    useItemTimer = 1.5f;
                    item1UseCount--;
                    if (item1UseCount == 0)
                    {
                        item1Stock = 0;
                    }
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
                    item2UseCount--;
                    if (item2UseCount == 0)
                    {
                        item2Stock = 0;
                    }
                }
                else if (item2Stock != 0 && useItemTimer >= 0)
                {
                    Debug.Log("アイテムクールタイム中・残り時間(" + useItemTimer + "秒)"); //消す
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                if (item3Stock != 0 && useItemTimer <= 0)
                {
                    Debug.Log(GetItemName(item3Stock) + "を使った"); //消す
                    Item(item3Stock);
                    useItemTimer = 1.5f;
                    item3UseCount--;
                    if (item3UseCount == 0)
                    {
                        item3Stock = 0;
                    }
                }
                else if (item3Stock != 0 && useItemTimer >= 0)
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
                Debug.Log("Item1 : " + GetItemName(item1Stock) + "  Item2 : " + GetItemName(item2Stock) + "  Item3 : " + GetItemName(item3Stock));
            }

            if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.LeftShift) && !Squat && !staminaOut)
            {
                m_ForwardSpeed = DashSpeed;
                m_SideSpeed = BackSideSpeed;
                m_BackSpeed = BackSideSpeed;
                run = true;
            }
            else if (!Squat && !staminaOut)
            {
                m_ForwardSpeed = WalkSpeed;
                m_SideSpeed = BackSideSpeed;
                m_BackSpeed = BackSideSpeed;
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
                m_Camera.transform.localEulerAngles += new Vector3(0f, 180f, 0f);
                m_Camera.fieldOfView = 60f;
                this.GetComponent<CapsuleCollider>().isTrigger = false;
                rb.useGravity = true;
                tagChange = false;
            }
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            playerLightOnOff = !playerLightOnOff;

            if (playerLightOnOff)
            {
                playerLight.SetActive(true);
            }
            if (!playerLightOnOff)
            {
                playerLight.SetActive(false);
            }
        }

        if (m_Enemies != null)
        {
            foreach (Enemy enemy in m_Enemies)
            {
                if (enemy == null)
                    continue;

                float distance = Vector3.Distance(
                    transform.position,
                    enemy.transform.position
                );

                if (distance <= 20.0f)
                {
                    enemy.flashLightHit = true;
                }
            }
        }

        if (enemyOutline)
        {
            if (m_Enemies != null)
            {
                foreach (Enemy enemy in m_Enemies)
                {
                    if (enemy == null)
                        continue;

                    Outline outline = enemy.GetComponent<Outline>();

                    if (outline != null)
                    {
                        outline.enabled = true;
                    }
                }
            }

            outlineTimer += Time.deltaTime;

            if (outlineTimer > ohudaTime)
            {
                enemyOutline = false;
            }
        }
        else
        {
            if (m_Enemies != null)
            {
                foreach (Enemy enemy in m_Enemies)
                {
                    if (enemy == null)
                        continue;

                    Outline outline = enemy.GetComponent<Outline>();

                    if (outline != null)
                    {
                        outline.enabled = false;
                    }
                }
            }

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

            // フラッシュライトを使った最初の1回だけEnemyをチェック
            if (!flashEnemyChecked)
            {
                if (m_Enemies != null)
                {
                    foreach (Enemy enemy in m_Enemies)
                    {
                        if (enemy == null)
                            continue;

                        Vector3 diff = transform.position - enemy.transform.position;
                        float distanceSqr = diff.sqrMagnitude;

                        // 20m以内のEnemyだけ反応
                        if (distanceSqr <= 20.0f * 20.0f)
                        {
                            enemy.flashLightHit = true;
                        }
                    }
                }

                flashEnemyChecked = true;
            }

            // Bloom開始
            if (flashFlg == 0)
            {
                bloom.enabled.value = true;
                bloom.intensity.value += Time.deltaTime * 50;

                if (bloom.intensity.value > 50)
                {
                    bloom.intensity.value = 50;
                    flashFlg = 1;
                }
            }

            // Bloom終了
            if (flashFlg == 1)
            {
                bloom.intensity.value -= Time.deltaTime * 50;

                if (bloom.intensity.value < 0)
                {
                    bloom.intensity.value = 0;
                    bloom.enabled.value = false;
                    flashFlg = 2;
                }
            }

            // 演出終了
            if (flashFlg == 2)
            {
                flashLight = false;
            }

            // フラッシュライトの時間終了
            if (flashLightTimer > flashLightTime)
            {
                if (m_Enemies != null)
                {
                    foreach (Enemy enemy in m_Enemies)
                    {
                        if (enemy == null)
                            continue;

                        enemy.flashLightHit = false;
                    }
                }

                flashLight = false;
            }
        }
        else
        {
            flashFlg = 0;
            flashLightTimer = 0;

            // 次回使用時にもう一度Enemyをチェック
            flashEnemyChecked = false;
        }

        if (!clockFlg)
        {
            clockPos = Vector3.zero;
        }

        if ((staminaOut || tagChange) && !staminam)
        {
            m_ForwardSpeed = SquatSpeed;
            m_BackSpeed = SquatSpeed;
            m_SideSpeed = SquatSpeed;
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

        UpdateFootstepSound(moveXZ);

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
            m_ForwardSpeed = SquatSpeed;
            m_BackSpeed = SquatSpeed;
            m_SideSpeed = SquatSpeed;
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
            m_ForwardSpeed = WalkSpeed;
            m_SideSpeed = BackSideSpeed;
            m_BackSpeed = BackSideSpeed;
            SquatMove = false;
        }
    }

    private float walkPlayPosition = 0f;
    private float runPlayPosition = 0f;

    private void UpdateFootstepSound(Vector3 moveXZ)
    {
        bool isMoving = moveXZ.sqrMagnitude > 0.01f;

        // 移動していない
        if (!isMoving || tagChange)
        {
            if (footstepSource.isPlaying)
            {
                // 現在の再生位置を保存
                if (footstepSource.clip == walkClip)
                {
                    walkPlayPosition = footstepSource.time;
                }
                else if (footstepSource.clip == runClip)
                {
                    runPlayPosition = footstepSource.time;
                }

                footstepSource.Pause();
            }

            return;
        }

        AudioClip targetClip;

        if (run)
        {
            targetClip = runClip;
        }
        else
        {
            targetClip = walkClip;
        }

        // 歩き・走りの音声が切り替わった
        if (footstepSource.clip != targetClip)
        {
            // 現在の音声の再生位置を保存
            if (footstepSource.clip == walkClip)
            {
                walkPlayPosition = footstepSource.time;
            }
            else if (footstepSource.clip == runClip)
            {
                runPlayPosition = footstepSource.time;
            }

            footstepSource.clip = targetClip;

            // それぞれの音声の前回の再生位置を復元
            if (targetClip == walkClip)
            {
                footstepSource.time = walkPlayPosition;
            }
            else if (targetClip == runClip)
            {
                footstepSource.time = runPlayPosition;
            }

            footstepSource.Play();
        }
        else if (!footstepSource.isPlaying)
        {
            // 音声が最後まで再生されていた場合
            if (footstepSource.time >= footstepSource.clip.length - 0.01f)
            {
                footstepSource.time = 0f;

                if (footstepSource.clip == walkClip)
                {
                    walkPlayPosition = 0f;
                }
                else if (footstepSource.clip == runClip)
                {
                    runPlayPosition = 0f;
                }
            }

            footstepSource.Play();
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
        if (i == 4)
        {
            clockFlg = true;
            clockPos = this.transform.position;
        }
        if (i == 5)
        {
            GameObject obj = Instantiate(flashStone);
            obj.transform.position = this.transform.position + Vector3.up * -0.98f;
            obj = null;
        }
    }

    private System.String GetItemName(int ID)
    {
        if (ID == 1)
        {
            return "御札";
        }
        if (ID == 2)
        {
            return "スタミナム";
        }
        if (ID == 3)
        {
            return "フラッシュライト";
        }
        if (ID == 4)
        {
            return "時計";
        }
        if (ID == 5)
        {
            return "光石";
        }
        else
        {
            return null;
        }
    }

    // 時計使った座標の取得
    public Vector3 GetClockPos()
    {
        return clockPos;
    }

    public bool GetCompassFlg()
    {
        return getCompass;
    }

    public bool GetGoalItemFlg()
    {
        return GoalItemFlg;
    }

    public int GetStockItem1()
    {
        return item1Stock;
    }
    public int GetStockItem2()
    {
        return item2Stock;
    }
    public int GetStockItem3()
    {
        return item3Stock;
    }

    public int GetItem1Count()
    {
        return item1UseCount;
    }
    public int GetItem2Count()
    {
        return item2UseCount;
    }
    public int GetItem3Count()
    {
        return item3UseCount;
    }
}
