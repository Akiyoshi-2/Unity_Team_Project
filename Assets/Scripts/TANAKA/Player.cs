using System;
using System.Collections; // コルーチンに必要
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityStandardAssets.Utility;

public class Player : MonoBehaviour
{
    public static Player Instance;

    #region 変数定義 - 基本コンポーネント
    private Rigidbody _rb;
    public Rigidbody rb { get { if (_rb == null) _rb = this.GetComponent<Rigidbody>(); return _rb; } }

    [SerializeField] private CurveControlledBob headBob_ = new CurveControlledBob();
    [SerializeField] private Camera m_Camera;
    [SerializeField] private GameObject playerLight;
    private bool playerLightOnOff = false;
    #endregion

    #region 変数定義 - 移動・スタミナ設定
    [Header("プレイヤー移動設定")]
    [SerializeField] private float WalkSpeed = 4f;
    [SerializeField] private float DashSpeed = 10f;
    [SerializeField] private float BackSideSpeed = 3f;
    [SerializeField] private float SquatSpeed = 2f;

    private float m_ForwardSpeed, m_BackSpeed, m_SideSpeed;
    [SerializeField] private float m_RotationSpeed = 2f;

    [Header("スタミナ設定")]
    [SerializeField] private float staminaTime = 4.0f;
    [SerializeField] private float staminaHealTime = 3.0f;
    private float stamina = 10.0f;
    private bool staminaOut = false;
    private bool run = false;

    private bool Squat = false;
    private bool SquatMove = false;
    #endregion

    #region 変数定義 - アイテムシステム
    [Header("アイテム設定")]
    [SerializeField] private float staminamTime = 10;
    [SerializeField] private float ohudaTime = 10;
    [SerializeField] private float flashRange = 20f;         // フラッシュの有効距離
    [SerializeField] private float flashStunDuration = 10f; // 敵が止まる時間
    [SerializeField] private GameObject flashStone;

    private int item1Stock = 0, item2Stock = 0, item3Stock = 0;
    private int item1UseCount = 0, item2UseCount = 0, item3UseCount = 0;
    private float useItemTimer = 0;

    private bool enemyOutline = false;
    private float outlineTimer = 0;
    private bool staminam = false;
    private float staminamTimer = 0;

    [NonSerialized] public bool clockFlg = false;
    [NonSerialized] public Vector3 clockPos = Vector3.zero;
    private bool getCompass = false;
    private bool GoalItemFlg = false;
    #endregion

    #region 変数定義 - 演出・敵・ギミック
    [SerializeField] private PostProcessVolume volume;

    private Enemy[] m_Enemies;
    private Vector3 camera_m, player_m;

    [Header("音響設定")]
    [SerializeField] private AudioClip walkClip;
    [SerializeField] private AudioClip runClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 1.0f;
    [SerializeField] private AudioClip itemGetClip;
    private AudioSource footstepSource;
    private AudioSource itemGetSource;

    private bool tagChange = false;
    private Vector3 posSave = Vector3.zero;
    private float dirStop;

    [Header("時計アイテム設定")]
    [SerializeField] private GameObject clockPrefab; // 作成した時計プレハブ

    // ドア操作用
    private int doorFlg = 0;
    private Transform door = null;
    #endregion

    private GOVManager m_GOVManager;

    #region 初期化
    private void Awake() => Instance = this;

    private void Start()
    {
        headBob_.Setup(m_Camera, 1.0f);
        m_GOVManager = FindFirstObjectByType<GOVManager>();

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.loop = true;
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 1f;
        footstepSource.volume = footstepVolume;

        itemGetSource = gameObject.AddComponent<AudioSource>();
        itemGetSource.playOnAwake = false;

        UpdateEnemyList();
    }

    public void UpdateEnemyList()
    {
        GameObject[] obs = GameObject.FindGameObjectsWithTag("Enemy");
        m_Enemies = new Enemy[obs.Length];
        for (int i = 0; i < obs.Length; i++) m_Enemies[i] = obs[i].GetComponent<Enemy>();
    }
    #endregion

    #region 更新処理 (Update)
    private void Update()
    {
        Vector3 moveXZ = Vector3.zero;
        float moveY = rb.linearVelocity.y;

        if (!tagChange)
        {
            HandleInputMovement(ref moveXZ);
            HandleInteraction();
            HandleInventoryInput();
            HandleSpeedAndSquat();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.F)) ExitHideout();
        }

        UpdateDoorAnimation();

        if (Input.GetKeyDown(KeyCode.E)) TogglePlayerLight();
        UpdateItemEffects();
        UpdateFootstepSound(moveXZ);

        rb.linearVelocity = new Vector3(moveXZ.x, moveY, moveXZ.z);
        HandleCameraRotation();
        if (SquatMove) UpdateSquatScale();

        this.transform.tag = tagChange ? "Invisible" : "Player";
    }
    #endregion

    #region 移動・回転
    private void HandleInputMovement(ref Vector3 moveXZ)
    {
        float h = 0, v = 0;
        if (Input.GetKey(KeyCode.W)) v += 1;
        if (Input.GetKey(KeyCode.S)) v -= 1;
        if (Input.GetKey(KeyCode.D)) h += 1;
        if (Input.GetKey(KeyCode.A)) h -= 1;

        moveXZ = (transform.forward * v * m_ForwardSpeed) + (transform.right * h * m_SideSpeed);

        if (v > 0 && run && !staminaOut && !staminam)
        {
            stamina -= Time.deltaTime * 10 / staminaTime;
            if (stamina <= 0) { stamina = 0; run = false; staminaOut = true; }
        }

        if (moveXZ.sqrMagnitude > 0.1f) m_Camera.transform.localPosition = headBob_.DoHeadBob(0.8f);
    }

    private void HandleCameraRotation()
    {
        float mx = Input.GetAxis("Mouse X") * m_RotationSpeed;
        float my = Input.GetAxis("Mouse Y") * m_RotationSpeed;

        if (!tagChange) // 通常時
        {
            player_m = new Vector3(0, transform.localEulerAngles.y + mx, 0);
            camera_m.x = Mathf.Clamp(camera_m.x - my, -80f, 80f);
        }
        else // 隠れている時
        {
            player_m.y = Mathf.Clamp(player_m.y + mx, dirStop - 30, dirStop + 30);

            // --- 修正箇所 ---
            // 第2引数（最小値）を -30f から 0f に変更すると、水平より上を向けなくなります。
            // 少しだけ下を向かせたい場合は 5f や 10f にしてください。
            camera_m.x = Mathf.Clamp(camera_m.x - my, 0f, 20f);
            // ----------------
        }
        transform.localEulerAngles = player_m;
        m_Camera.transform.localEulerAngles = new Vector3(camera_m.x, 0, 0);
    }
    #endregion

    #region インタラクト・ドア開閉 (Fキー)
    private void HandleInteraction()
    {
        if (!Input.GetKeyDown(KeyCode.F)) return;

        RaycastHit hit;
        if (Physics.Raycast(m_Camera.transform.position, m_Camera.transform.forward, out hit, 2.5f))
        {
            if (hit.collider.CompareTag("item")) PickUpItem(hit.collider.gameObject);
            else if (hit.collider.CompareTag("GoalItem")) { GoalItemFlg = true; hit.collider.gameObject.SetActive(false); }
            else if (hit.collider.CompareTag("HideBox")) EnterHideout(hit);
            else if (hit.collider.CompareTag("Door") || hit.collider.CompareTag("wDoor")) SetupDoor(hit);
            else if (hit.collider.CompareTag("Light"))
            {
                var l = hit.transform.GetComponent<FlashingLight>();
                if (l) l.LightOn = true;
                hit.transform.GetComponent<Light>().enabled = true;
            }
        }
    }

    private void SetupDoor(RaycastHit hit)
    {
        door = hit.transform;
        if (hit.collider.CompareTag("Door"))
            doorFlg = (door.localEulerAngles.y == 0) ? 1 : 2;
        else
            doorFlg = (door.localPosition.z == 0.75) ? 3 : 4;
    }

    private void UpdateDoorAnimation()
    {
        if (door == null || doorFlg == 0) return;

        if (doorFlg == 1)
        {
            door.GetComponent<Collider>().isTrigger = true;
            door.transform.localEulerAngles += new Vector3(0, 220 * Time.deltaTime, 0);
            if (door.localEulerAngles.y >= 110) { door.localEulerAngles = new Vector3(0, 110, 0); door.GetComponent<Collider>().isTrigger = false; doorFlg = 0; }
        }
        else if (doorFlg == 2)
        {
            door.GetComponent<Collider>().isTrigger = true;
            door.transform.localEulerAngles -= new Vector3(0, 220 * Time.deltaTime, 0);
            if (door.localEulerAngles.y <= 0 || door.localEulerAngles.y >= 150) { door.localEulerAngles = new Vector3(0, 0, 0); door.GetComponent<Collider>().isTrigger = false; doorFlg = 0; }
        }
        else if (doorFlg == 3)
        {
            door.localPosition -= new Vector3(0, 0, 3.0f * Time.deltaTime);
            if (door.localPosition.z <= -0.75f) { door.localPosition = new Vector3(door.localPosition.x, door.localPosition.y, -0.75f); doorFlg = 0; }
        }
        else if (doorFlg == 4)
        {
            door.localPosition += new Vector3(0, 0, 3.0f * Time.deltaTime);
            if (door.localPosition.z >= 0.75f) { door.localPosition = new Vector3(door.localPosition.x, door.localPosition.y, 0.75f); doorFlg = 0; }
        }
    }
    #endregion

    #region アイテム
    private void HandleInventoryInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) UseItemInSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) UseItemInSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) UseItemInSlot(3);
        if (useItemTimer > 0) useItemTimer -= Time.deltaTime;
    }

    private void UseItemInSlot(int slot)
    {
        int id = (slot == 1) ? item1Stock : (slot == 2) ? item2Stock : item3Stock;
        if (id != 0 && useItemTimer <= 0)
        {
            ActivateItemEffect(id);
            useItemTimer = 1.5f;
            if (slot == 1) { item1UseCount--; if (item1UseCount <= 0) item1Stock = 0; }
            else if (slot == 2) { item2UseCount--; if (item2UseCount <= 0) item2Stock = 0; }
            else { item3UseCount--; if (item3UseCount <= 0) item3Stock = 0; }
        }
    }

    private void ActivateItemEffect(int id)
    {
        switch (id)
        {
            case 1: enemyOutline = true; outlineTimer = 0; break;
            case 2: staminam = true; staminamTimer = 0; break;
            case 3: ExecuteFlashEffect(); break;
            case 4:
                if (clockPrefab != null)
                {
                    // プレイヤーの 1.2m 前方の座標を計算
                    Vector3 spawnPos = transform.position + transform.forward * 1.2f;
                    // 高さをプレイヤーの足元付近に合わせる（微調整してください）
                    spawnPos.y = transform.position.y - 0.9f;

                    Instantiate(clockPrefab, spawnPos, Quaternion.identity);
                }
                break;
            case 5: Instantiate(flashStone, transform.position + Vector3.up * -0.98f, Quaternion.identity); break;
        }
    }

    private void ExecuteFlashEffect()
    {
        // 重要：使用する瞬間にシーン内の最新の敵リストを取得する
        UpdateEnemyList();

        foreach (Enemy enemy in m_Enemies)
        {
            if (enemy != null)
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);

                if (dist <= flashRange)
                {
                    StartCoroutine(StunEnemyRoutine(enemy));
                }
            }
        }
    }

    private IEnumerator StunEnemyRoutine(Enemy enemy)
    {
        enemy.flashLightHit = true; // Enemy.cs側の移動停止・当たり判定消失
        yield return new WaitForSeconds(flashStunDuration);
        if (enemy != null) enemy.flashLightHit = false; // 復帰
    }

    private void UpdateItemEffects()
    {
        if (m_Enemies != null) foreach (Enemy e in m_Enemies) if (e != null && e.GetComponent<Outline>()) e.GetComponent<Outline>().enabled = enemyOutline;
        if (enemyOutline) { outlineTimer += Time.deltaTime; if (outlineTimer > ohudaTime) enemyOutline = false; }

        if (staminam)
        {
            staminamTimer += Time.deltaTime; staminaOut = false; stamina = Mathf.Min(stamina + Time.deltaTime * 10, 10.0f);
            if (staminamTimer > staminamTime) staminam = false;
        }

        if (!staminam && !run && stamina < 10.0f)
        {
            stamina += Time.deltaTime * 10 / staminaHealTime;
            if (stamina >= 10.0f) { stamina = 10.0f; staminaOut = false; }
        }
    }
    #endregion

    #region 隠れる・しゃがみ
    private void EnterHideout(RaycastHit hit)
    {
        posSave = transform.position;
        transform.localScale = Vector3.zero;
        GetComponent<CapsuleCollider>().isTrigger = true;

        // 位置の調整
        transform.position = hit.transform.position + Vector3.up * 0.53f + Vector3.left * 0.15f;

        dirStop = hit.transform.localEulerAngles.y - 90; // もし逆なら +90 を -90 にするか、+270にする

        // 現在のプレイヤーの向きを、隠れ場所の正面に合わせる
        player_m = new Vector3(0, dirStop, 0);
        transform.localEulerAngles = player_m;

        rb.useGravity = false;
        tagChange = true;

        m_Camera.fieldOfView = 30f;
    }

    private void ExitHideout()
    {
        transform.localScale = Squat ? new Vector3(1, 0.5f, 1) : Vector3.one; transform.position = posSave;
        m_Camera.transform.localEulerAngles += new Vector3(0f, 180f, 0f); m_Camera.fieldOfView = 60f;
        GetComponent<CapsuleCollider>().isTrigger = false; rb.useGravity = true; tagChange = false;
    }

    private void HandleSpeedAndSquat()
    {
        if (Input.GetKey(KeyCode.W) && Input.GetKey(KeyCode.LeftShift) && !Squat && !staminaOut) { m_ForwardSpeed = DashSpeed; run = true; }
        else if (!Squat && !staminaOut) { m_ForwardSpeed = WalkSpeed; run = false; }
        else { m_ForwardSpeed = SquatSpeed; run = false; }
        m_BackSpeed = m_SideSpeed = BackSideSpeed;

        if (Input.GetKeyDown(KeyCode.C) && !staminaOut) { if (!Squat) transform.position += Vector3.down * 0.5f; Squat = !Squat; SquatMove = true; }
    }

    private void UpdateSquatScale()
    {
        if (Squat) { transform.localScale = new Vector3(1, 0.5f, 1); SquatMove = false; }
        else
        {
            if (Physics.Raycast(transform.position, Vector3.up, 1.2f)) { Squat = true; SquatMove = false; return; }
            transform.localScale = Vector3.one; SquatMove = false;
        }
    }
    #endregion

    #region 音響
    private float walkPos = 0, runPos = 0;
    private void UpdateFootstepSound(Vector3 move)
    {
        if (move.sqrMagnitude < 0.01f || tagChange)
        {
            if (footstepSource.isPlaying)
            {
                if (footstepSource.clip == walkClip) walkPos = footstepSource.time; else runPos = footstepSource.time;
                footstepSource.Pause();
            }
            return;
        }
        AudioClip target = run ? runClip : walkClip;
        if (footstepSource.clip != target)
        {
            if (footstepSource.clip == walkClip) walkPos = footstepSource.time; else runPos = footstepSource.time;
            footstepSource.clip = target; footstepSource.time = (target == walkClip) ? walkPos : runPos; footstepSource.Play();
        }
        else if (!footstepSource.isPlaying) footstepSource.Play();
    }
    #endregion

    #region 取得・外部参照
    private void PickUpItem(GameObject obj)
    {
        string n = obj.name; int id = 0, count = 0;
        if (n.Contains("御札")) { id = 1; count = 1; }
        else if (n.Contains("スタミナム")) { id = 2; count = 1; }
        else if (n.Contains("フラッシュライト")) { id = 3; count = 1; }
        else if (n.Contains("時計")) { id = 4; count = 1; }
        else if (n.Contains("光石")) { id = 5; count = 5; }
        else if (n.Contains("コンパス")) { getCompass = true; obj.SetActive(false); return; }

        obj.SetActive(false); itemGetSource.PlayOneShot(itemGetClip);
        if (item1Stock == 0) { item1Stock = id; item1UseCount = count; }
        else if (item2Stock == 0) { item2Stock = id; item2UseCount = count; }
        else if (item3Stock == 0) { item3Stock = id; item3UseCount = count; }
    }

    private void TogglePlayerLight() { playerLightOnOff = !playerLightOnOff; playerLight.SetActive(playerLightOnOff); }

    public float staminaNum() => stamina;
    public bool staminaOutbool() => staminaOut;
    public Vector3 GetClockPos() => clockPos;
    public bool GetCompassFlg() => getCompass;
    public bool GetGoalItemFlg() => GoalItemFlg;
    public int GetStockItem1() => item1Stock;
    public int GetStockItem2() => item2Stock;
    public int GetStockItem3() => item3Stock;
    public int GetItem1Count() => item1UseCount;
    public int GetItem2Count() => item2UseCount;
    public int GetItem3Count() => item3UseCount;
    #endregion
}