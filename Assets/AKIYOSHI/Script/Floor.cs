using UnityEngine;

public class Floor : MonoBehaviour
{
    public GameObject[] CenterFloors;
    public GameObject[] CornerFloors;
    public GameObject[] SideFloors;
    public GameObject GoalFloor;

    public int width = 5;
    public int height = 5;

    public float size = 20f;

    GameObject GetRandom(GameObject[] prefabs)
    {
        return prefabs[
            Random.Range(0, prefabs.Length)
        ];
    }

    void Start()
    {
        GenerateFloor();
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
                    // ¶‰º
                    if (x == 0 && z == 0)
                    {
                        rot = Quaternion.Euler(0, 0, 0);
                    }
                    // ¶ã
                    else if (x == 0 && z == height - 1)
                    {
                        rot = Quaternion.Euler(0, 90, 0);
                    }
                    // ‰Eã
                    else if (x == width - 1 && z == height - 1)
                    {
                        rot = Quaternion.Euler(0, 180, 0);
                    }
                    // ‰E‰º
                    else if (x == width - 1 && z == 0)
                    {
                        rot = Quaternion.Euler(0, 270, 0);
                    }
                }
                else if (isEdge && !isGoal && !isSpecialCenter)
                {
                    // ‰º•Ó
                    if (z == 0 && x >= 1 && x <= width - 2)
                    {
                        rot = Quaternion.Euler(0, 0, 0);
                    }
                    // ¶•Ó
                    else if (x == 0 && z >= 1 && z <= height - 2)
                    {
                        rot = Quaternion.Euler(0, 90, 0);
                    }
                    // ã•Ó
                    else if (z == height - 1 && x >= 1 && x <= width - 2)
                    {
                        rot = Quaternion.Euler(0, 180, 0);
                    }
                    // ‰E•Ó
                    else if (x == width - 1 && z >= 1 && z <= height - 2)
                    {
                        rot = Quaternion.Euler(0, 270, 0);
                    }
                }

                Instantiate(prefabToSpawn, pos, rot);
            }
        }
    }
}