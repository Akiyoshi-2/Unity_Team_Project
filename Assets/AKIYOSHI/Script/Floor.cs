using Unity.AI.Navigation;
using UnityEngine;
using System.Collections;

public class Floor : MonoBehaviour
{
    public GameObject[] CenterFloors;
    public GameObject[] CornerFloors;
    public GameObject[] SideFloors;
    public GameObject GoalFloor;

    public NavMeshSurface navMeshSurface;

    public int width = 5;
    public int height = 5;

    public float size = 44f;

    GameObject GetRandom(GameObject[] prefabs)
    {
        return prefabs[
            Random.Range(0, prefabs.Length)
        ];

    }

    IEnumerator Start()
    {
        // 1. まず床をすべて生成する
        GenerateFloor();

        // 2. 1フレーム待機（重要！）
        // これを挟むことで、Unityが生成された全オブジェクトの衝突判定（Collider）を正しく認識します
        yield return null;
        Physics.SyncTransforms(); // Collider情報を強制同期

        yield return new WaitForFixedUpdate(); // 物理系の反映を待つ

        if (navMeshSurface != null)
        {
            // 3. 全てのフロアが完全に配置された状態で、NavMeshを再構築する
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh has been rebuilt.");
        }
    }

    void GenerateFloor()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                GameObject prefabToSpawn;

                bool isSpecialCenter = (x == 0 && z == 2);

                bool isGoal = (x == width - 1 && z == 2);

                bool isCorner =
                    (x == 0 && z == 0) ||
                    (x == 0 && z == height - 1) ||
                    (x == width - 1 && z == 0) ||
                    (x == width - 1 && z == height - 1);

                bool isEdge =
                    x == 0 ||
                    x == width - 1 ||
                    z == 0 ||
                    z == height - 1;

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

                Vector3 pos = new Vector3(
                    x * size,
                    0,
                    z * size
                );

                Quaternion rot = Quaternion.identity;

                if (isCorner)
                {
                    // 左下
                    if (x == 0 && z == 0)
                    {
                        rot = Quaternion.Euler(0, 0, 0);
                    }
                    // 左上
                    else if (x == 0 && z == height - 1)
                    {
                        rot = Quaternion.Euler(0, 90, 0);
                    }
                    // 右上
                    else if (x == width - 1 && z == height - 1)
                    {
                        rot = Quaternion.Euler(0, 180, 0);
                    }
                    // 右下
                    else if (x == width - 1 && z == 0)
                    {
                        rot = Quaternion.Euler(0, 270, 0);
                    }
                }
                else if (isEdge && !isGoal && !isSpecialCenter)
                {
                    // 下辺
                    if (z == 0 && x >= 1 && x <= width - 2)
                    {
                        rot = Quaternion.Euler(0, 0, 0);
                    }
                    // 左辺
                    else if (x == 0 && z >= 1 && z <= height - 2)
                    {
                        rot = Quaternion.Euler(0, 90, 0);
                    }
                    // 上辺
                    else if (z == height - 1 && x >= 1 && x <= width - 2)
                    {
                        rot = Quaternion.Euler(0, 180, 0);
                    }
                    // 右辺
                    else if (x == width - 1 && z >= 1 && z <= height - 2)
                    {
                        rot = Quaternion.Euler(0, 270, 0);
                    }
                }

                Instantiate(prefabToSpawn, pos, rot);

                GameObject obj = Instantiate(prefabToSpawn, pos, rot);

                Debug.Log(obj.transform.Find("Floor").localPosition);

            }
        }
    }
}