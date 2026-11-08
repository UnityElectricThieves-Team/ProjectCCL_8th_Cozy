using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 아트가 넣은 장식·배경 폴더를 훑어, 아직 상품이 없는 폴더마다 상품(<see cref="ShopItemDefinition"/>)을 만들고
/// 상품 목록(<see cref="ShopCatalog"/>)에 넣는다. 아트 폴더는 <c>Resources/Decorations/&lt;이름&gt;/decoration.prefab</c>,
/// <c>Resources/Backgrounds/&lt;이름&gt;/background.prefab</c>과 같은 폴더의 <c>store_thumbnail.png</c> 모양이다.
///
/// 손으로 수십 개를 만들지 않으려고 둔다. 여러 번 눌러도 이미 상품이 있는 폴더는 건드리지 않는다 —
/// "상품이 있다"는 그 폴더의 프리팹을 가리키는 상품이 있다는 뜻이라, 옛 이름(shop_item_N_...)의 상품도 그대로 남는다.
///
/// 새 상품의 id는 <c>deco_</c>/<c>bg_</c> + 폴더 이름 소문자다. id는 저장 키라 한번 만들면 바꾸지 않는다.
/// 이름은 번역 표에서 이 id로 찾으므로(<see cref="ShopItemDefinition.NameKey"/>), 새 폴더를 받으면 번역 표에도 그 줄을 넣어야 한다.
/// 가격은 임시값으로 넣는다. 확정 가격은 상품 에셋에서 고친다.
///
/// 열린 씬의 <see cref="ShopSystem"/>·<see cref="BackgroundSystem"/>에 목록이 비어 있으면 이 목록을 걸어 준다. 씬 저장은 사람이 한다.
/// </summary>
public static class ShopItemGenerator
{
    private const string DECORATION_ROOT = "Assets/Resources/Decorations";
    private const string BACKGROUND_ROOT = "Assets/Resources/Backgrounds";
    private const string ITEM_FOLDER = "Assets/ShopItems";
    private const string CATALOG_PATH = ITEM_FOLDER + "/ShopCatalog.asset";
    private const string STRINGS_PATH = "Assets/Localization/Strings.json";

    // 기획이 가격을 정하기 전까지 쓰는 임시 가격(하트).
    private const int TEMP_PRICE = 10;

    [MenuItem("Cozy/Shop/아트 폴더에서 상품 만들기")]
    public static void Generate()
    {
        ShopCatalog catalog = LoadOrCreateCatalog();

        // 이미 있는 상품 전부. 목록에 없던 상품도 여기서 목록에 넣는다(목록 에셋이 생기기 전에 만든 상품들).
        var decorations = new List<ShopItemDefinition>();
        var backgrounds = new List<ShopItemDefinition>();
        var usedPrefabs = new HashSet<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ShopItemDefinition)))
        {
            var item = AssetDatabase.LoadAssetAtPath<ShopItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null) continue;
            if (item.backgroundPrefab != null) { backgrounds.Add(item); usedPrefabs.Add(item.backgroundPrefab); }
            else if (item.decorationPrefab != null) { decorations.Add(item); usedPrefabs.Add(item.decorationPrefab); }
        }

        int created = 0;
        // 에셋을 하나 만들 때마다 임포트하지 않고 끝에서 한 번에 한다.
        AssetDatabase.StartAssetEditing();
        try
        {
            created += CreateMissing(DECORATION_ROOT, "decoration.prefab", "deco_", "Deco_", false, usedPrefabs, decorations);
            created += CreateMissing(BACKGROUND_ROOT, "background.prefab", "bg_", "Bg_", true, usedPrefabs, backgrounds);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        // 기존 목록의 순서를 지키고, 새로 찾은 것만 뒤에 붙인다.
        catalog.decorations = Merge(catalog.decorations, decorations);
        catalog.backgrounds = Merge(catalog.backgrounds, backgrounds);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        WarnMissingNameKeys(catalog);

        bool sceneChanged = AssignToScene<ShopSystem>(catalog) | AssignToScene<BackgroundSystem>(catalog);

        Debug.Log($"[{nameof(ShopItemGenerator)}] 새 상품 {created}개를 만들었습니다. 목록: 장식 {catalog.decorations.Length}개, 배경 {catalog.backgrounds.Length}개." +
                  (sceneChanged ? " 씬의 시스템에 목록을 걸었습니다 — 씬을 저장하세요." : ""), catalog);
    }

    private static ShopCatalog LoadOrCreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CATALOG_PATH);
        if (catalog != null) return catalog;

        catalog = ScriptableObject.CreateInstance<ShopCatalog>();
        AssetDatabase.CreateAsset(catalog, CATALOG_PATH);
        return catalog;
    }

    // root 아래 폴더마다 prefabName이 있고 아직 그 프리팹을 가리키는 상품이 없으면 상품을 만든다. 만든 개수를 돌려준다.
    private static int CreateMissing(string root, string prefabName, string idPrefix, string filePrefix, bool isBackground,
        HashSet<GameObject> usedPrefabs, List<ShopItemDefinition> results)
    {
        int created = 0;
        foreach (string folder in AssetDatabase.GetSubFolders(root))
        {
            string folderName = Path.GetFileName(folder);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(ShopItemGenerator)}] '{folder}'에 {prefabName}이 없어 건너뜁니다.");
                continue;
            }
            if (usedPrefabs.Contains(prefab)) continue;

            // 같은 이름의 상품 파일이 이미 있으면 덮어쓰지 않는다. 덮어쓰면 고쳐 둔 가격·아이콘이 초기화된다.
            string itemPath = ITEM_FOLDER + "/" + filePrefix + folderName + ".asset";
            if (File.Exists(itemPath))
            {
                Debug.LogWarning($"[{nameof(ShopItemGenerator)}] '{itemPath}'가 이미 있지만 '{folder}'의 프리팹을 가리키지 않아 건너뜁니다. 상품 에셋을 확인하세요.");
                continue;
            }

            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/store_thumbnail.png");
            if (icon == null)
                Debug.LogWarning($"[{nameof(ShopItemGenerator)}] '{folder}'에 store_thumbnail.png가 없어 아이콘을 비워 둡니다. 아트에 요청하세요.");

            var item = ScriptableObject.CreateInstance<ShopItemDefinition>();
            item.id = idPrefix + folderName.ToLowerInvariant();
            item.icon = icon;
            item.price = TEMP_PRICE;
            if (isBackground) item.backgroundPrefab = prefab;
            else item.decorationPrefab = prefab;

            AssetDatabase.CreateAsset(item, itemPath);
            usedPrefabs.Add(prefab);
            results.Add(item);
            created++;
        }
        return created;
    }

    // 목록의 상품마다 번역 표에 이름 줄이 있는지 본다. 번역 표는 사람이 줄 단위로 관리하므로 여기서 쓰지 않는다.
    private static void WarnMissingNameKeys(ShopCatalog catalog)
    {
        if (!File.Exists(STRINGS_PATH))
        {
            Debug.LogWarning($"[{nameof(ShopItemGenerator)}] 번역 표 '{STRINGS_PATH}'가 없어 이름 줄을 확인하지 못했습니다.");
            return;
        }

        string strings = File.ReadAllText(STRINGS_PATH);
        WarnMissingNameKeys(catalog.decorations, strings);
        WarnMissingNameKeys(catalog.backgrounds, strings);
    }

    private static void WarnMissingNameKeys(ShopItemDefinition[] items, string strings)
    {
        foreach (var item in items)
        {
            if (!strings.Contains("\"" + item.NameKey + "\""))
                Debug.LogWarning($"[{nameof(ShopItemGenerator)}] 번역 표에 '{item.NameKey}' 줄이 없습니다. {STRINGS_PATH}에 넣으세요.", item);
        }
    }

    private static ShopItemDefinition[] Merge(ShopItemDefinition[] existing, List<ShopItemDefinition> found)
    {
        var merged = new List<ShopItemDefinition>();
        if (existing != null)
        {
            foreach (var item in existing)
            {
                if (item != null && !merged.Contains(item)) merged.Add(item);
            }
        }
        foreach (var item in found)
        {
            if (!merged.Contains(item)) merged.Add(item);
        }
        return merged.ToArray();
    }

    // 열린 씬의 시스템에 목록이 비어 있으면 건다. 바꿨으면 true.
    private static bool AssignToScene<T>(ShopCatalog catalog) where T : MonoBehaviour
    {
        var system = Object.FindFirstObjectByType<T>();
        if (system == null) return false;

        var serialized = new SerializedObject(system);
        SerializedProperty property = serialized.FindProperty("_catalog");
        if (property == null || property.objectReferenceValue != null) return false;

        property.objectReferenceValue = catalog;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
        return true;
    }
}
