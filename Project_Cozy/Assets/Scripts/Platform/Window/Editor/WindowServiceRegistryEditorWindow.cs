#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class WindowServiceRegistryEditorWindow : EditorWindow
{
    private const string DefaultAssetPath = "Assets/Resources/WindowServiceRegistry.bytes";
    private const int BinaryVersion = 2;
    private const string DeprecatedResizeBindingKey = "camera_fitter";

    private static readonly Dictionary<string, string> BindingKeyByTypeFullName = new Dictionary<string, string>();

    [Serializable]
    private sealed class RegistryData
    {
        public List<string> GlobalEnabledBindingKeys = new List<string>();
        public List<SceneEntry> SceneEntries = new List<SceneEntry>();
        public List<EnvironmentEntry> EnvironmentEntries = new List<EnvironmentEntry>();

        public List<string> LegacyGlobalMonoBehaviourTypeNames = new List<string>();
        public List<string> LegacyGlobalPlainTypeNames = new List<string>();
    }

    [Serializable]
    private sealed class SceneEntry
    {
        public string ScenePath;
        public List<string> EnabledBindingKeys = new List<string>();

        public List<string> LegacyMonoBehaviourTypeNames = new List<string>();
        public List<string> LegacyPlainTypeNames = new List<string>();
    }

    [Serializable]
    private sealed class EnvironmentEntry
    {
        public string EnvironmentKey;
        public List<string> EnabledBindingKeys = new List<string>();
    }

    private RegistryData _data;
    private string _assetPath = DefaultAssetPath;
    private string _selectedScenePath;
    private string _selectedEnvironmentKey = "default";
    private Vector2 _scroll;

    private MonoScript _scriptToAdd;

    [MenuItem("Tools/Window Services/Registry Editor")]
    public static void Open()
    {
        var window = GetWindow<WindowServiceRegistryEditorWindow>("Window Service Registry");
        window.minSize = new Vector2(720f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadOrCreate();
        if (string.IsNullOrEmpty(_selectedScenePath))
            _selectedScenePath = EditorSceneManager.GetActiveScene().path;
    }

    private void OnGUI()
    {
        DrawToolbar();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawGlobalSection();
        EditorGUILayout.Space(10);
        DrawSceneSection();
        EditorGUILayout.Space(10);
        DrawEnvironmentSection();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("저장 (Resources 바이너리)", GUILayout.Height(32)))
            SaveBinary();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Registry Binary Path", EditorStyles.boldLabel);
        _assetPath = EditorGUILayout.TextField(_assetPath);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("불러오기")) LoadOrCreate();
        if (GUILayout.Button("새로 만들기")) _data = new RegistryData();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawGlobalSection()
    {
        EnsureData();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("1) 전체 씬 공통 서비스", EditorStyles.boldLabel);

        _scriptToAdd = (MonoScript)EditorGUILayout.ObjectField("Script", _scriptToAdd, typeof(MonoScript), false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("공통 MonoBehaviour 추가"))
            TryAddScriptToGlobal(isMonoBehaviour: true);
        if (GUILayout.Button("공통 Plain Class 추가"))
            TryAddScriptToGlobal(isMonoBehaviour: false);
        EditorGUILayout.EndHorizontal();

        DrawTypeList("Global Enabled Bindings", _data.GlobalEnabledBindingKeys);

        EditorGUILayout.EndVertical();
    }

    private void DrawSceneSection()
    {
        EnsureData();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("2) 씬별 서비스", EditorStyles.boldLabel);

        DrawSceneSelector();

        _scriptToAdd = (MonoScript)EditorGUILayout.ObjectField("Script", _scriptToAdd, typeof(MonoScript), false);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("선택 씬 MonoBehaviour 추가"))
            TryAddScriptToScene(isMonoBehaviour: true);
        if (GUILayout.Button("선택 씬 Plain Class 추가"))
            TryAddScriptToScene(isMonoBehaviour: false);
        EditorGUILayout.EndHorizontal();

        var entry = GetOrCreateSceneEntry(_selectedScenePath);
        DrawTypeList($"Scene Enabled Bindings ({Path.GetFileNameWithoutExtension(_selectedScenePath)})", entry.EnabledBindingKeys);

        EditorGUILayout.EndVertical();
    }

    private void DrawSceneSelector()
    {
        var scenes = GetKnownScenePaths();
        if (scenes.Count == 0)
        {
            EditorGUILayout.HelpBox("씬 경로를 찾지 못했습니다. 씬을 한 번 저장하세요.", MessageType.Info);
            return;
        }

        if (string.IsNullOrEmpty(_selectedScenePath) || !scenes.Contains(_selectedScenePath))
            _selectedScenePath = scenes[0];

        int currentIndex = Mathf.Max(0, scenes.IndexOf(_selectedScenePath));
        int nextIndex = EditorGUILayout.Popup("Scene", currentIndex, scenes.ConvertAll(Path.GetFileName).ToArray());
        _selectedScenePath = scenes[nextIndex];

        EditorGUILayout.LabelField("Scene Path", _selectedScenePath);
    }

    private void DrawTypeList(string title, List<string> typeNames)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        if (typeNames == null || typeNames.Count == 0)
        {
            EditorGUILayout.LabelField("(none)");
            return;
        }

        for (int i = 0; i < typeNames.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(typeNames[i], GUILayout.Height(16));
            if (GUILayout.Button("삭제", GUILayout.Width(56)))
            {
                typeNames.RemoveAt(i);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawEnvironmentSection()
    {
        EnsureData();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("3) 환경별 서비스", EditorStyles.boldLabel);

        _selectedEnvironmentKey = EditorGUILayout.TextField("Environment Key", _selectedEnvironmentKey);

        _scriptToAdd = (MonoScript)EditorGUILayout.ObjectField("Script", _scriptToAdd, typeof(MonoScript), false);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("환경 MonoBehaviour 추가"))
            TryAddScriptToEnvironment(isMonoBehaviour: true);
        if (GUILayout.Button("환경 Plain Class 추가"))
            TryAddScriptToEnvironment(isMonoBehaviour: false);
        EditorGUILayout.EndHorizontal();

        var entry = GetOrCreateEnvironmentEntry(_selectedEnvironmentKey);
        DrawTypeList($"Environment Enabled Bindings ({entry.EnvironmentKey})", entry.EnabledBindingKeys);

        EditorGUILayout.EndVertical();
    }

    private void TryAddScriptToGlobal(bool isMonoBehaviour)
    {
        if (!TryGetValidatedBindingKey(_scriptToAdd, isMonoBehaviour, out var bindingKey))
            return;

        AddUnique(_data.GlobalEnabledBindingKeys, bindingKey);
    }

    private void TryAddScriptToScene(bool isMonoBehaviour)
    {
        if (string.IsNullOrEmpty(_selectedScenePath))
        {
            ShowNotification(new GUIContent("씬을 먼저 선택하세요."));
            return;
        }

        if (!TryGetValidatedBindingKey(_scriptToAdd, isMonoBehaviour, out var bindingKey))
            return;

        var entry = GetOrCreateSceneEntry(_selectedScenePath);
        AddUnique(entry.EnabledBindingKeys, bindingKey);
    }

    private void TryAddScriptToEnvironment(bool isMonoBehaviour)
    {
        if (string.IsNullOrWhiteSpace(_selectedEnvironmentKey))
        {
            ShowNotification(new GUIContent("환경 키를 입력하세요."));
            return;
        }

        if (!TryGetValidatedBindingKey(_scriptToAdd, isMonoBehaviour, out var bindingKey))
            return;

        var entry = GetOrCreateEnvironmentEntry(_selectedEnvironmentKey);
        AddUnique(entry.EnabledBindingKeys, bindingKey);
    }

    private bool TryGetValidatedBindingKey(MonoScript script, bool isMonoBehaviour, out string bindingKey)
    {
        bindingKey = null;

        if (script == null)
        {
            ShowNotification(new GUIContent("스크립트를 선택하세요."));
            return false;
        }

        var type = script.GetClass();
        if (type == null)
        {
            ShowNotification(new GUIContent("유효한 타입을 찾을 수 없습니다."));
            return false;
        }

        bool isMbType = typeof(MonoBehaviour).IsAssignableFrom(type);
        if (isMonoBehaviour != isMbType)
        {
            ShowNotification(new GUIContent(isMonoBehaviour
                ? "MonoBehaviour 타입만 추가할 수 있습니다."
                : "Plain Class 타입만 추가할 수 있습니다."));
            return false;
        }

        if (!BindingKeyByTypeFullName.TryGetValue(type.FullName ?? type.Name, out bindingKey))
        {
            ShowNotification(new GUIContent("코드 바인딩에 등록된 구현 타입만 추가할 수 있습니다."));
            return false;
        }

        if (!isMbType && type.GetConstructor(Type.EmptyTypes) == null)
        {
            ShowNotification(new GUIContent("Plain Class는 기본 생성자가 필요합니다."));
            return false;
        }

        return true;
    }

    private void SaveBinary()
    {
        EnsureData();
        MigrateLegacyTypeNames(_data);
        RemoveDeprecatedBindings(_data);

        var bytes = SerializeData(_data);
        var absolutePath = Path.GetFullPath(_assetPath);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(absolutePath, bytes);
        AssetDatabase.ImportAsset(_assetPath);
        AssetDatabase.Refresh();

        ShowNotification(new GUIContent($"저장 완료: {_assetPath}"));
    }

    private void LoadOrCreate()
    {
        EnsureData();

        if (!File.Exists(_assetPath))
        {
            _data = new RegistryData();
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(_assetPath);
            _data = DeserializeData(bytes);
            MigrateLegacyTypeNames(_data);
            RemoveDeprecatedBindings(_data);
        }
        catch (Exception ex)
        {
            _data = new RegistryData();
            Debug.LogWarning($"[WindowServiceRegistryEditorWindow] 레지스트리 로드 실패: {ex.Message}");
        }
    }

    private static void RemoveDeprecatedBindings(RegistryData data)
    {
        if (data == null)
            return;

        RemoveAll(data.GlobalEnabledBindingKeys, DeprecatedResizeBindingKey);

        for (int i = 0; i < data.SceneEntries.Count; i++)
        {
            var entry = data.SceneEntries[i];
            if (entry == null)
                continue;

            RemoveAll(entry.EnabledBindingKeys, DeprecatedResizeBindingKey);
        }

        for (int i = 0; i < data.EnvironmentEntries.Count; i++)
        {
            var entry = data.EnvironmentEntries[i];
            if (entry == null)
                continue;

            RemoveAll(entry.EnabledBindingKeys, DeprecatedResizeBindingKey);
        }
    }

    private static void RemoveAll(List<string> list, string target)
    {
        if (list == null || string.IsNullOrEmpty(target))
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i], target, StringComparison.OrdinalIgnoreCase))
                list.RemoveAt(i);
        }
    }

    private void EnsureData()
    {
        if (_data == null)
            _data = new RegistryData();
    }

    private SceneEntry GetOrCreateSceneEntry(string scenePath)
    {
        for (int i = 0; i < _data.SceneEntries.Count; i++)
        {
            var entry = _data.SceneEntries[i];
            if (entry != null && string.Equals(entry.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        var newEntry = new SceneEntry { ScenePath = scenePath };
        _data.SceneEntries.Add(newEntry);
        return newEntry;
    }

    private EnvironmentEntry GetOrCreateEnvironmentEntry(string environmentKey)
    {
        if (string.IsNullOrWhiteSpace(environmentKey))
            environmentKey = "default";

        for (int i = 0; i < _data.EnvironmentEntries.Count; i++)
        {
            var entry = _data.EnvironmentEntries[i];
            if (entry != null && string.Equals(entry.EnvironmentKey, environmentKey, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        var newEntry = new EnvironmentEntry { EnvironmentKey = environmentKey };
        _data.EnvironmentEntries.Add(newEntry);
        return newEntry;
    }

    private static void MigrateLegacyTypeNames(RegistryData data)
    {
        if (data == null)
            return;

        MigrateTypes(data.LegacyGlobalMonoBehaviourTypeNames, data.GlobalEnabledBindingKeys);
        MigrateTypes(data.LegacyGlobalPlainTypeNames, data.GlobalEnabledBindingKeys);

        for (int i = 0; i < data.SceneEntries.Count; i++)
        {
            var entry = data.SceneEntries[i];
            if (entry == null)
                continue;

            MigrateTypes(entry.LegacyMonoBehaviourTypeNames, entry.EnabledBindingKeys);
            MigrateTypes(entry.LegacyPlainTypeNames, entry.EnabledBindingKeys);
        }
    }

    private static void MigrateTypes(List<string> legacyTypeNames, List<string> output)
    {
        if (legacyTypeNames == null || output == null)
            return;

        for (int i = 0; i < legacyTypeNames.Count; i++)
        {
            var typeName = legacyTypeNames[i];
            if (string.IsNullOrWhiteSpace(typeName))
                continue;

            var type = Type.GetType(typeName);
            if (type == null)
                continue;

            if (!BindingKeyByTypeFullName.TryGetValue(type.FullName ?? type.Name, out var bindingKey))
                continue;

            AddUnique(output, bindingKey);
        }
    }

    private static byte[] SerializeData(RegistryData data)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write(BinaryVersion);
            WriteStringList(writer, data.GlobalEnabledBindingKeys);

            writer.Write(data.SceneEntries != null ? data.SceneEntries.Count : 0);
            if (data.SceneEntries != null)
            {
                for (int i = 0; i < data.SceneEntries.Count; i++)
                {
                    var entry = data.SceneEntries[i] ?? new SceneEntry();
                    writer.Write(entry.ScenePath ?? string.Empty);
                    WriteStringList(writer, entry.EnabledBindingKeys);
                }
            }

            writer.Write(data.EnvironmentEntries != null ? data.EnvironmentEntries.Count : 0);
            if (data.EnvironmentEntries != null)
            {
                for (int i = 0; i < data.EnvironmentEntries.Count; i++)
                {
                    var entry = data.EnvironmentEntries[i] ?? new EnvironmentEntry();
                    writer.Write(entry.EnvironmentKey ?? string.Empty);
                    WriteStringList(writer, entry.EnabledBindingKeys);
                }
            }

            writer.Flush();
            return ms.ToArray();
        }
    }

    private static RegistryData DeserializeData(byte[] bytes)
    {
        var data = new RegistryData();
        if (bytes == null || bytes.Length == 0)
            return data;

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            int version = reader.ReadInt32();
            if (version == 1)
            {
                data.LegacyGlobalMonoBehaviourTypeNames = ReadStringList(reader);
                data.LegacyGlobalPlainTypeNames = ReadStringList(reader);

                int sceneCountV1 = reader.ReadInt32();
                for (int i = 0; i < sceneCountV1; i++)
                {
                    var legacyEntry = new SceneEntry
                    {
                        ScenePath = reader.ReadString(),
                        LegacyMonoBehaviourTypeNames = ReadStringList(reader),
                        LegacyPlainTypeNames = ReadStringList(reader),
                    };
                    data.SceneEntries.Add(legacyEntry);
                }

                return data;
            }

            if (version != BinaryVersion)
                throw new InvalidOperationException($"Unsupported WindowServiceRegistry binary version: {version}");

            data.GlobalEnabledBindingKeys = ReadStringList(reader);

            int sceneCount = reader.ReadInt32();
            for (int i = 0; i < sceneCount; i++)
            {
                var sceneEntry = new SceneEntry
                {
                    ScenePath = reader.ReadString(),
                    EnabledBindingKeys = ReadStringList(reader),
                };
                data.SceneEntries.Add(sceneEntry);
            }

            int environmentCount = reader.ReadInt32();
            for (int i = 0; i < environmentCount; i++)
            {
                var environmentEntry = new EnvironmentEntry
                {
                    EnvironmentKey = reader.ReadString(),
                    EnabledBindingKeys = ReadStringList(reader),
                };
                data.EnvironmentEntries.Add(environmentEntry);
            }
        }

        return data;
    }

    private static void WriteStringList(BinaryWriter writer, List<string> list)
    {
        writer.Write(list != null ? list.Count : 0);
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
            writer.Write(list[i] ?? string.Empty);
    }

    private static List<string> ReadStringList(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
            list.Add(reader.ReadString());

        return list;
    }

    private static List<string> GetKnownScenePaths()
    {
        var list = new List<string>();

        var active = EditorSceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(active))
            list.Add(active);

        var buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            var path = buildScenes[i].path;
            if (!string.IsNullOrEmpty(path) && !list.Contains(path))
                list.Add(path);
        }

        return list;
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (list == null || string.IsNullOrWhiteSpace(value) || list.Contains(value))
            return;

        list.Add(value);
    }
}
#endif
