using UnityEngine;

public class Floor : MonoBehaviour
{
    [Header("フロアプレハブ")]
    public GameObject[] CenterFloors;
    public GameObject[] CornerFloors;
    public GameObject[] SideFloors;
    public GameObject GoalFloor;

    [Header("マップ設定")]
    public int width = 5;
    public int height = 5;
    public float size = 44f;

    void Start()
    {
        // ナビメッシュは使わないので、即座に生成を開始するだけでOKです
        GenerateFloor();
    }

    GameObject GetRandom(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0) return null;
        return prefabs[Random.Range(0, prefabs.Length)];
    }

    void GenerateFloor()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                GameObject prefabToSpawn = null;

                // 特殊な中心地点 (影廊の開始地点のような場所)
                bool isSpecialCenter = (x == 0 && z == 2);
                // ゴール地点
                bool isGoal = (x == width - 1 && z == 2);

                // 角の判定
                bool isCorner =
                    (x == 0 && z == 0) ||
                    (x == 0 && z == height - 1) ||
                    (x == width - 1 && z == 0) ||
                    (x == width - 1 && z == height - 1);

                // 外周（エッジ）の判定
                bool isEdge =
                    x == 0 ||
                    x == width - 1 ||
                    z == 0 ||
                    z == height - 1;

                // --- プレハブの選定 ---
                if (isSpecialCenter)
                {
                    prefabToSpawn = GetRandom(CenterFloors);
                }
                else if (isGoal)
                {
                    prefabToSpawn = GoalFloor;
                }
                else if (isCorner)
                {
                    prefabToSpawn = GetRandom(CornerFloors);
                }
                else if (isEdge)
                {
                    prefabToSpawn = GetRandom(SideFloors);
                }
                else
                {
                    prefabToSpawn = GetRandom(CenterFloors);
                }

                if (prefabToSpawn == null) continue;

                // --- 座標の計算 ---
                Vector3 pos = new Vector3(x * size, 0, z * size);
                Quaternion rot = Quaternion.identity;

                // --- 回転の計算 ---
                if (isCorner)
                {
                    if (x == 0 && z == 0) rot = Quaternion.Euler(0, 0, 0); // 左下
                    else if (x == 0 && z == height - 1) rot = Quaternion.Euler(0, 90, 0); // 左上
                    else if (x == width - 1 && z == height - 1) rot = Quaternion.Euler(0, 180, 0); // 右上
                    else if (x == width - 1 && z == 0) rot = Quaternion.Euler(0, 270, 0); // 右下
                }
                else if (isEdge && !isGoal && !isSpecialCenter)
                {
                    if (z == 0) rot = Quaternion.Euler(0, 0, 0); // 下辺
                    else if (x == 0) rot = Quaternion.Euler(0, 90, 0); // 左辺
                    else if (z == height - 1) rot = Quaternion.Euler(0, 180, 0); // 上辺
                    else if (x == width - 1) rot = Quaternion.Euler(0, 270, 0); // 右辺
                }

                // --- 生成 (修正ポイント：1回だけInstantiateする) ---
                GameObject obj = Instantiate(prefabToSpawn, pos, rot);

                // ヒエラルキーが散らからないようにこのオブジェクトの子にする（任意）
                obj.transform.SetParent(this.transform);

                // デバッグログ（プレハブ内に"Floor"という子オブジェクトがある前提のコード）
                Transform floorChild = obj.transform.Find("Floor");
                if (floorChild != null)
                {
                    Debug.Log($"Floor {x},{z} position: {floorChild.localPosition}");
                }
            }
        }
    }
}