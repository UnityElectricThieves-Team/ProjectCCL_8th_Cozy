using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>장식 프리팹을 게임 배율 그대로 한 줄에 놓는 아트 확인용 씬 생성 메뉴.</summary>
public static class DecorationArtSampleSceneBuilder
{
    private const string DecorationRoot = "Assets/Resources/Decorations";
    private const string SceneFolder = "Assets/Scenes/ArtSample";
    private const float GapPixels = 80f;
    private const float PixelsPerUnit = 100f;

    [MenuItem("Cozy/Art/장식 전체 나열 Sample Scene 만들기")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        List<string> paths = FindDecorationPaths();
        if (paths.Count == 0)
        {
            Debug.LogWarning("장식 프리팹을 찾지 못했습니다: " + DecorationRoot);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Art Decorations - Game Scale");
        float rightEdge = PlaceDecorations(paths, root.transform, scene);
        Camera camera = CreateCamera(rightEdge);
        CreateLighting();
        SaveScene(scene);
        FrameSceneView(camera);
        Debug.Log($"장식 {paths.Count}종을 원본 프리팹 배율로 나열했습니다. Play에서 하단 스크롤바로 좌우 이동합니다.");
    }

    private static List<string> FindDecorationPaths()
    {
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { DecorationRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path) == "decoration.prefab") paths.Add(path);
        }
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static float PlaceDecorations(List<string> paths, Transform parent, Scene scene)
    {
        float cursor = 0f;
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = Path.GetFileName(Path.GetDirectoryName(path));
            instance.transform.SetParent(parent, false);
            Bounds bounds = GetVisibleBounds(instance);
            // 배율은 게임의 DecorationLayer처럼 프리팹 값을 그대로 둡니다. 아래 끝만 지면에 맞춥니다.
            instance.transform.position += new Vector3(cursor - bounds.min.x, -bounds.min.y, 0);
            cursor += bounds.size.x + GapPixels / PixelsPerUnit;
        }
        return cursor - GapPixels / PixelsPerUnit;
    }

    private static Bounds GetVisibleBounds(GameObject instance)
    {
        Bounds bounds = default;
        bool found = false;
        foreach (SpriteRenderer renderer in instance.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        if (!found) throw new InvalidOperationException("표시할 스프라이트가 없습니다: " + instance.name);
        return bounds;
    }

    private static Camera CreateCamera(float rightEdge)
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.22f, 0.25f, 0.3f, 1);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<DecorationArtSampleCamera>().Configure(0, rightEdge);
        return camera;
    }

    private static void CreateLighting()
    {
        var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1;
    }

    private static void SaveScene(Scene scene)
    {
        EnsureFolder(SceneFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath(SceneFolder + "/DecorationLineupSample.unity");
        if (!EditorSceneManager.SaveScene(scene, path))
            throw new IOException("샘플 씬을 저장하지 못했습니다: " + path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void FrameSceneView(Camera camera)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;
        view.in2DMode = true;
        Vector3 center = camera.transform.position;
        center.z = 0;
        view.LookAt(center, Quaternion.identity, camera.orthographicSize, true);
    }

    [MenuItem("Cozy/Art/장식 전체 나열 Sample Scene 만들기", true)]
    private static bool CanCreateScene() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
