using System.Collections.Generic;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>();

    private void Start()
    {
        // 1. 指定された「Item」フォルダ内のみを全自動で探索してリスト化
#if UNITY_EDITOR
        itemPrefabs.Clear();

        // あなたが作成したフォルダの場所をピンポイントで指定（これで一瞬で処理が終わります）
        string targetFolderPath = "Assets/AKIYOSHI/Prefab/Item";
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { targetFolderPath });

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // Itemタグが付いているものだけをリストに追加
            if (prefab != null && prefab.CompareTag("Item"))
            {
                if (!itemPrefabs.Contains(prefab))
                {
                    itemPrefabs.Add(prefab);
                }
            }
        }
#endif

        // 2. 集まった中からランダムに1つスポーン
        SpawnRandomItem();
    }

    public void SpawnRandomItem()
    {
        if (itemPrefabs == null || itemPrefabs.Count == 0) return;

        // 登録されている中からランダムに1つ選択
        int randomIndex = Random.Range(0, itemPrefabs.Count);
        GameObject selectedPrefab = itemPrefabs[randomIndex];

        // 選択されたプレハブをスポーン
        if (selectedPrefab.CompareTag("Item"))
        {
            Instantiate(selectedPrefab, transform.position, transform.rotation);
        }
    }
}