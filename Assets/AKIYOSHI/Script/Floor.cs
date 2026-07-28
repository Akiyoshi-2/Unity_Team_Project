using System.Collections.Generic;
using UnityEngine;

public class Floor : MonoBehaviour
{
    [Header("フロアプレハブ")]
    public GameObject[] CenterFloors;
    public GameObject[] CornerFloors;
    public GameObject[] SideFloors;
    public GameObject GoalFloor;

    private List<GameObject> centerPool = new List<GameObject>();
    private List<GameObject> sidePool = new List<GameObject>();
    private List<GameObject> cornerPool = new List<GameObject>();

    [Header("マップ設定")]
    public int width = 5;
    public int height = 5;
    public float size = 44f;

    void Start()
    {
        ResetPools();
        GenerateFloor();
    }

    void ResetPools()
    {
        centerPool = new List<GameObject>(CenterFloors);
        sidePool = new List<GameObject>(SideFloors);
        cornerPool = new List<GameObject>(CornerFloors);
    }

    GameObject GetRandom(ref List<GameObject> pool, GameObject[] source)
    {
        if (pool.Count == 0)
        {
            pool.AddRange(source);
        }

        int index = Random.Range(0, pool.Count);
        GameObject prefab = pool[index];
        pool.RemoveAt(index);

        return prefab;
    }

    void GenerateFloor()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                GameObject prefabToSpawn = null;

                // 中心地点
                bool isSpecialCenter = (x == 0 && z == 2);
                // ゴール地点
                bool isGoal = (x == width - 1 && z == 2);

                // 角の判定
                bool isCorner =
                    (x == 0 && z == 0) ||
                    (x == 0 && z == height - 1) ||
                    (x == width - 1 && z == 0) ||
                    (x == width - 1 && z == height - 1);

                // 外周の判定
                bool isEdge =
                    x == 0 ||
                    x == width - 1 ||
                    z == 0 ||
                    z == height - 1;

                // プレハブの選定
                if (isSpecialCenter)
                {
                    prefabToSpawn = GetRandom(ref centerPool, CenterFloors);
                }
                else if (isGoal)
                {
                    prefabToSpawn = GoalFloor;
                }
                else if (isCorner)
                {
                    prefabToSpawn = GetRandom(ref cornerPool, CornerFloors);
                }
                else if (isEdge)
                {
                    prefabToSpawn = GetRandom(ref sidePool, SideFloors);
                }
                else
                {
                    prefabToSpawn = GetRandom(ref centerPool, CenterFloors);
                }

                if (prefabToSpawn == null) continue;

                // 座標の計算
                Vector3 pos = new Vector3(x * size, 0, z * size);
                Quaternion rot = Quaternion.identity;

                // 回転の計算
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

                // 生成
                GameObject obj = Instantiate(prefabToSpawn, pos, rot);

                // ヒエラルキーが散らからないようにこのオブジェクトの子にする
                obj.transform.SetParent(this.transform);

                // デバッグログ
                Transform floorChild = obj.transform.Find("Floor");
                if (floorChild != null)
                {
                    Debug.Log($"Floor {x},{z} position: {floorChild.localPosition}");
                }
            }
        }
    }
}