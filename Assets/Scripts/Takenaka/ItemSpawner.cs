using System.Collections.Generic;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>();

    private void Start()
    {
#if UNITY_EDITOR
        itemPrefabs.Clear();

        // パスを画像に合わせて「Model」を追加しました
        string targetFolderPath = "Assets/AKIYOSHI/Model/Prefab/Item";

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { targetFolderPath });

        if (guids.Length == 0)
        {
            Debug.LogError($"プレハブが見つかりません！パスが正しいか確認してください: {targetFolderPath}");
        }

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // タグが「item」になっているかチェック
            if (prefab != null && prefab.CompareTag("item"))
            {
                if (!itemPrefabs.Contains(prefab))
                {
                    itemPrefabs.Add(prefab);
                }
            }
            else if (prefab != null)
            {
                Debug.LogWarning($"プレハブ '{prefab.name}' は見つかりましたが、タグが 'item' ではありません。現在のタグ: {prefab.tag}");
            }
        }
#endif

        SpawnRandomItem();
    }

    public void SpawnRandomItem()
    {
        if (itemPrefabs.Count == 0)
        {
            Debug.LogError("スポーンできるアイテムがリストにありません。");
            return;
        }

        int randomIndex = Random.Range(0, itemPrefabs.Count);
        GameObject selectedPrefab = itemPrefabs[randomIndex];
        Instantiate(selectedPrefab, transform.position, transform.rotation);
    }
}