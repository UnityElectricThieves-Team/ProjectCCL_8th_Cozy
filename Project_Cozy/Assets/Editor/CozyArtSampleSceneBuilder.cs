using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CozyArtSampleSceneBuilder
{
    private const int CanvasWidthPixels = 3849;
    private const int CanvasHeightPixels = 360;
    private const float PixelsPerUnit = 100f;
    private const float ViewportHalfHeight = CanvasHeightPixels / PixelsPerUnit / 2f;
    private const float GroundY = -ViewportHalfHeight;
    private const string ArtFolder = "Assets/Art/CozyArtSample";
    private const string SceneFolder = "Assets/Scenes/ArtSamples";
    private const string PreviewFolder = ArtFolder + "/Previews";

    private static readonly Color CameraColor = new(0.98f, 0.96f, 0.92f, 1f);

    [MenuItem("Tools/Cozy Art/Rebuild Sample Scenes")]
    public static void Build()
    {
        EnsureFoldersExist();
        ConfigureBackground("warm-cozy-BG2.png");
        ConfigureSheet("type2-1.png", Type21Slices());
        ConfigureSheet("type2-2.png", Type22Slices());

        BuildSample("CozyArt_BG2_Type2_1_Sample", "warm-cozy-BG2.png", Type21Placements());
        BuildSample("CozyArt_BG2_Type2_2_Sample", "warm-cozy-BG2.png", Type22Placements());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Cozy art sample scenes and 3849x360 previews were rebuilt.");
    }

    private static void EnsureFoldersExist()
    {
        EnsureFolder("Assets/Scenes", "ArtSamples");
        EnsureFolder(ArtFolder, "Previews");
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        string path = $"{parent}/{folderName}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void ConfigureBackground(string fileName)
    {
        TextureImporter importer = GetTextureImporter(fileName);
        ApplyCommonTextureSettings(importer);
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
    }

    private static void ConfigureSheet(string fileName, Slice[] slices)
    {
        TextureImporter importer = GetTextureImporter(fileName);
        ApplyCommonTextureSettings(importer);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.SaveAndReimport();

        SpriteDataProviderFactories factories = new();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(CreateSpriteRects(importer, slices));
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static TextureImporter GetTextureImporter(string fileName)
    {
        string path = $"{ArtFolder}/{fileName}";
        return AssetImporter.GetAtPath(path) as TextureImporter
            ?? throw new InvalidOperationException($"Texture importer was not found: {path}");
    }

    private static void ApplyCommonTextureSettings(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    private static SpriteRect[] CreateSpriteRects(TextureImporter importer, Slice[] slices)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
        SpriteRect[] spriteRects = new SpriteRect[slices.Length];

        for (int index = 0; index < slices.Length; index++)
        {
            Slice slice = slices[index];
            spriteRects[index] = new SpriteRect
            {
                name = slice.Name,
                rect = slice.ToBottomLeftRect(texture.height),
                alignment = SpriteAlignment.Custom,
                pivot = slice.Pivot,
                spriteID = GUID.Generate()
            };
        }

        return spriteRects;
    }

    private static void BuildSample(string sampleName, string backgroundFile, Placement[] placements)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera camera = CreateCamera();
        CreateRollingBackground(backgroundFile);
        CreateDecorations(placements);

        string scenePath = $"{SceneFolder}/{sampleName}.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        RenderPreview(camera, $"{PreviewFolder}/{sampleName}.png");
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new("Preview Camera - 3849x360");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = CanvasHeightPixels / PixelsPerUnit / 2f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = CameraColor;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        return camera;
    }

    private static void CreateRollingBackground(string backgroundFile)
    {
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{backgroundFile}");
        if (background == null)
        {
            throw new InvalidOperationException($"Background sprite was not found: {backgroundFile}");
        }

        GameObject strip = new("Rolling Background - 3 Tiles");
        float tileWidth = background.rect.width / PixelsPerUnit;

        for (int tileIndex = -1; tileIndex <= 1; tileIndex++)
        {
            GameObject tile = CreateSpriteObject($"BG Tile {tileIndex + 2}", background, -100);
            tile.transform.SetParent(strip.transform);
            tile.transform.position = new Vector3(tileIndex * tileWidth, 0f, 0f);
        }
    }

    private static void CreateDecorations(Placement[] placements)
    {
        GameObject wallRoot = new("Wall Decorations");
        GameObject floorRoot = new("Floor Decorations");

        foreach (Placement placement in placements)
        {
            Sprite sprite = LoadSprite(placement.Sheet, placement.SpriteName);
            GameObject decoration = CreateSpriteObject(placement.SpriteName, sprite, placement.SortingOrder);
            decoration.transform.SetParent(placement.IsWallDecoration ? wallRoot.transform : floorRoot.transform);
            decoration.transform.position = new Vector3(placement.X, placement.Y, 0f);
            decoration.transform.localScale = Vector3.one * placement.Scale;
        }
    }

    private static Sprite LoadSprite(string sheet, string spriteName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath($"{ArtFolder}/{sheet}");
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite && sprite.name == spriteName)
            {
                return sprite;
            }
        }

        throw new InvalidOperationException($"Sprite was not found: {sheet}/{spriteName}");
    }

    private static GameObject CreateSpriteObject(string name, Sprite sprite, int sortingOrder)
    {
        GameObject spriteObject = new(name);
        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return spriteObject;
    }

    private static void RenderPreview(Camera camera, string assetPath)
    {
        RenderTexture renderTexture = new(CanvasWidthPixels, CanvasHeightPixels, 24, RenderTextureFormat.ARGB32);
        Texture2D preview = new(CanvasWidthPixels, CanvasHeightPixels, TextureFormat.RGBA32, false);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            preview.ReadPixels(new Rect(0, 0, CanvasWidthPixels, CanvasHeightPixels), 0, 0);
            preview.Apply();
            File.WriteAllBytes(assetPath, preview.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(preview);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    private static Slice[] Type21Slices() => new[]
    {
        new Slice("Type2A_Fireplace", 63, 97, 187, 230, 2),
        new Slice("Type2A_Armchair", 276, 158, 182, 170, 3),
        new Slice("Type2A_Sofa", 480, 163, 258, 165, 2),
        new Slice("Type2A_Cabinet", 755, 169, 107, 158, 2),
        new Slice("Type2A_Bookcase", 882, 148, 104, 179, 2),
        new Slice("Type2A_Vanity", 1016, 150, 106, 178, 2),
        new Slice("Type2A_Window", 1153, 156, 164, 171),
        new Slice("Type2A_Door", 74, 427, 151, 203, 2),
        new Slice("Type2A_CoatRack", 254, 421, 131, 210, 2),
        new Slice("Type2A_FloorLamp", 428, 431, 92, 199, 2),
        new Slice("Type2A_WallShelf", 555, 456, 148, 83),
        new Slice("Type2A_PetBed", 708, 475, 120, 158, 3),
        new Slice("Type2A_GiftStack", 847, 488, 123, 141, 2),
        new Slice("Type2A_Chest", 993, 519, 130, 110, 2),
        new Slice("Type2A_Rug", 1137, 536, 190, 92, 4)
    };

    private static Slice[] Type22Slices() => new[]
    {
        new Slice("Type2B_Fireplace", 52, 28, 148, 180, 2),
        new Slice("Type2B_Armchair", 220, 63, 169, 145, 2),
        new Slice("Type2B_Sofa", 399, 76, 211, 133, 3),
        new Slice("Type2B_Cabinet", 630, 75, 113, 134, 3),
        new Slice("Type2B_Bookcase", 776, 50, 132, 159, 2),
        new Slice("Type2B_SideTable", 938, 74, 106, 134, 2),
        new Slice("Type2B_Clock", 1078, 45, 92, 110),
        new Slice("Type2B_Window", 1204, 40, 135, 169),
        new Slice("Type2B_Door", 56, 271, 141, 207, 2),
        new Slice("Type2B_CoatRack", 260, 277, 83, 201, 2),
        new Slice("Type2B_FloorLamp", 415, 288, 73, 189, 2),
        new Slice("Type2B_WallShelf", 540, 314, 143, 113),
        new Slice("Type2B_Tree", 711, 268, 132, 211, 2),
        new Slice("Type2B_GiftStack", 866, 335, 145, 142, 2),
        new Slice("Type2B_Ottoman", 1035, 378, 137, 97, 3),
        new Slice("Type2B_RockingChair", 1184, 302, 146, 175, 3),
        new Slice("Type2B_ExtraGifts", 52, 552, 163, 154, 2)
    };

    private static Placement[] Type21Placements() => new[]
    {
        Placement.Floor("type2-1.png", "Type2A_Fireplace", -17.8f, 12),
        Placement.Floor("type2-1.png", "Type2A_Armchair", -15.1f, 13),
        Placement.Floor("type2-1.png", "Type2A_Sofa", -12.25f, 13),
        Placement.Floor("type2-1.png", "Type2A_Cabinet", -9.55f, 12),
        Placement.Floor("type2-1.png", "Type2A_Bookcase", -7.55f, 11),
        Placement.Floor("type2-1.png", "Type2A_Vanity", -5.55f, 12),
        Placement.Wall("type2-1.png", "Type2A_Window", -3.3f, -0.05f, 1),
        Placement.Floor("type2-1.png", "Type2A_Door", -0.85f, 10),
        Placement.Floor("type2-1.png", "Type2A_CoatRack", 1.65f, 13),
        Placement.Floor("type2-1.png", "Type2A_FloorLamp", 3.9f, 13),
        Placement.Wall("type2-1.png", "Type2A_WallShelf", 6.1f, 0.62f, 3),
        Placement.Floor("type2-1.png", "Type2A_PetBed", 8.25f, 13, 1.05f),
        Placement.Floor("type2-1.png", "Type2A_GiftStack", 10.55f, 15),
        Placement.Floor("type2-1.png", "Type2A_Chest", 13.1f, 13),
        Placement.Floor("type2-1.png", "Type2A_Rug", 16.55f, 2)
    };

    private static Placement[] Type22Placements() => new[]
    {
        Placement.Floor("type2-2.png", "Type2B_Fireplace", -17.8f, 12),
        Placement.Floor("type2-2.png", "Type2B_Armchair", -15.25f, 13),
        Placement.Floor("type2-2.png", "Type2B_Sofa", -12.4f, 13),
        Placement.Floor("type2-2.png", "Type2B_Cabinet", -9.65f, 12),
        Placement.Floor("type2-2.png", "Type2B_Bookcase", -7.55f, 11),
        Placement.Floor("type2-2.png", "Type2B_SideTable", -5.25f, 13),
        Placement.Wall("type2-2.png", "Type2B_Clock", -3.75f, 0.54f, 3),
        Placement.Wall("type2-2.png", "Type2B_Window", -1.8f, -0.06f, 1),
        Placement.Floor("type2-2.png", "Type2B_Door", 0.45f, 10),
        Placement.Floor("type2-2.png", "Type2B_CoatRack", 2.7f, 13),
        Placement.Floor("type2-2.png", "Type2B_FloorLamp", 4.8f, 13),
        Placement.Wall("type2-2.png", "Type2B_WallShelf", 6.9f, 0.5f, 3),
        Placement.Floor("type2-2.png", "Type2B_Tree", 9.0f, 13),
        Placement.Floor("type2-2.png", "Type2B_GiftStack", 11.2f, 15),
        Placement.Floor("type2-2.png", "Type2B_Ottoman", 13.2f, 13),
        Placement.Floor("type2-2.png", "Type2B_RockingChair", 15.2f, 13),
        Placement.Floor("type2-2.png", "Type2B_ExtraGifts", 17.75f, 15)
    };

    private readonly struct Slice
    {
        public Slice(string name, int x, int top, int width, int height, int bottomPadding = 0)
        {
            Name = name;
            X = x;
            Top = top;
            Width = width;
            Height = height;
            BottomPadding = bottomPadding;
        }

        public string Name { get; }
        public Vector2 Pivot => new(0.5f, BottomPadding / (float)Height);
        private int X { get; }
        private int Top { get; }
        private int Width { get; }
        private int Height { get; }
        private int BottomPadding { get; }

        public Rect ToBottomLeftRect(int textureHeight)
        {
            return new Rect(X, textureHeight - Top - Height, Width, Height);
        }
    }

    private readonly struct Placement
    {
        private Placement(
            string sheet,
            string spriteName,
            float x,
            float y,
            int sortingOrder,
            float scale,
            bool isWallDecoration)
        {
            Sheet = sheet;
            SpriteName = spriteName;
            X = x;
            Y = y;
            SortingOrder = sortingOrder;
            Scale = scale;
            IsWallDecoration = isWallDecoration;
        }

        public string Sheet { get; }
        public string SpriteName { get; }
        public float X { get; }
        public float Y { get; }
        public int SortingOrder { get; }
        public float Scale { get; }
        public bool IsWallDecoration { get; }

        public static Placement Floor(
            string sheet,
            string spriteName,
            float x,
            int sortingOrder,
            float scale = 1f)
        {
            return new Placement(sheet, spriteName, x, GroundY, sortingOrder, scale, false);
        }

        public static Placement Wall(
            string sheet,
            string spriteName,
            float x,
            float y,
            int sortingOrder,
            float scale = 1f)
        {
            return new Placement(sheet, spriteName, x, y, sortingOrder, scale, true);
        }
    }
}
