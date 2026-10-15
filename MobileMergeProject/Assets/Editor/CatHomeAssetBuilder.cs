using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Code.CatHome;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CatHomeAssetBuilder
{
    public const string Folder = "Assets/Resources/BlockBlast/CatHome";
    public const string AtlasPath = Folder + "/CatGrowthAtlas.png";
    public const string CatalogPath = Folder + "/CatGrowthCatalog.asset";
    public const string PrefabPath = Folder + "/CatResidents.prefab";

    [MenuItem("Tools/Catch the Cats/Build Cat Home Assets")]
    public static void Build()
    {
        SliceAtlas();
        CatGrowthCatalog catalog = AssetDatabase.LoadAssetAtPath<CatGrowthCatalog>(CatalogPath);
        bool createCatalog = catalog == null;
        if (createCatalog)
            catalog = ScriptableObject.CreateInstance<CatGrowthCatalog>();

        catalog.adultLineCount = 2;

        Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath)
            .OfType<Sprite>().ToDictionary(sprite => sprite.name);
        string[] blockNames = { "BlackCatBlockSprite", "WhiteCatBlockSprite", "SphynxCatBlockSprite", "CatBlockSprite" };
        Vector2[] positions = { new Vector2(0.852f, 0.365f), new Vector2(0.848f, 0.199f),
            new Vector2(0.112f, 0.282f), new Vector2(0.085f, 0.377f) };
        Vector2[] sizes = { new Vector2(0.069f, 0.053f), new Vector2(0.14f, 0.1f),
            new Vector2(0.14f, 0.092f), new Vector2(0.078f, 0.060f) };
        CatGrowthCatalog.CatDefinition[] definitions = new CatGrowthCatalog.CatDefinition[4];
        for (int i = 0; i < definitions.Length; i++)
        {
            HomeCatType type = (HomeCatType)i;
            CatGrowthCatalog.CatDefinition previous = catalog.cats.FirstOrDefault(cat => cat.type == type);
            definitions[i] = new CatGrowthCatalog.CatDefinition
            {
                type = type,
                blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/BlockBlast/" + blockNames[i] + ".png"),
                kitten = sprites[type + "_Kitten"], juvenile = sprites[type + "_Juvenile"], adult = sprites[type + "_Adult"],
                homePosition = previous != null ? previous.homePosition : positions[i],
                adultSize = previous != null ? previous.adultSize : sizes[i]
            };
        }
        catalog.cats = definitions;
        if (createCatalog)
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        EditorUtility.SetDirty(catalog);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            GameObject root = new GameObject("CatResidents", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                RectTransform rootRect = (RectTransform)root.transform;
                Stretch(rootRect);
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                foreach (CatGrowthCatalog.CatDefinition cat in definitions)
                {
                    GameObject home = new GameObject(cat.type + "Cat", typeof(RectTransform), typeof(CatResidentView));
                    home.transform.SetParent(root.transform, false);
                    RectTransform homeRect = (RectTransform)home.transform;
                    homeRect.pivot = new Vector2(0.5f, 0f);
                    homeRect.anchorMin = cat.homePosition - new Vector2(cat.adultSize.x * 0.5f, 0f);
                    homeRect.anchorMax = cat.homePosition + new Vector2(cat.adultSize.x * 0.5f, cat.adultSize.y);
                    homeRect.offsetMin = homeRect.offsetMax = Vector2.zero;
                    GameObject body = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    body.transform.SetParent(home.transform, false);
                    RectTransform bodyRect = (RectTransform)body.transform;
                    bodyRect.pivot = new Vector2(0.5f, 0f);
                    Stretch(bodyRect);
                    Image image = body.GetComponent<Image>();
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    CatResidentView view = home.GetComponent<CatResidentView>();
                    view.Configure(cat.type, image);
                    view.SetProgress(catalog, cat, 0, false);
                }
                SetUILayer(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Cat home atlas, catalog and residents prefab are ready.");
    }

    public static void BuildAndVerify()
    {
        if (Application.isBatchMode)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        Build();
        CatHomeValidation.Run();
    }

    private static void SliceAtlas()
    {
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 256;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            texture.LoadImage(File.ReadAllBytes(AtlasPath));
            Color32[] pixels = texture.GetPixels32();
            if (pixels[0].a != 0)
                throw new InvalidOperationException("Cat atlas must have a genuinely transparent background.");
            float[] columnEdges = { 0f, 0.265f, 0.515f, 0.762f, 1f };
            float[] rowEdges = { 0f, 0.342f, 0.64f, 1f };
            string[] ages = { "Kitten", "Juvenile", "Adult" };
            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            Dictionary<string, SpriteRect> previous = provider.GetSpriteRects().ToDictionary(rect => rect.name);
            List<SpriteRect> rects = new List<SpriteRect>();
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < 4; column++)
            {
                int x0 = Mathf.RoundToInt(columnEdges[column] * texture.width);
                int x1 = Mathf.RoundToInt(columnEdges[column + 1] * texture.width);
                int y0 = Mathf.RoundToInt((1f - rowEdges[row + 1]) * texture.height);
                int y1 = Mathf.RoundToInt((1f - rowEdges[row]) * texture.height);
                int minX = x1, minY = y1, maxX = x0, maxY = y0;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    if (pixels[y * texture.width + x].a < 32)
                        continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                if (maxX - minX < 50 || maxY - minY < 50)
                    throw new InvalidOperationException("Missing cat in atlas cell.");
                string name = (HomeCatType)column + "_" + ages[row];
                rects.Add(new SpriteRect
                {
                    name = name,
                    rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    alignment = SpriteAlignment.BottomCenter, pivot = new Vector2(0.5f, 0f),
                    spriteID = previous.TryGetValue(name, out SpriteRect old) ? old.spriteID : GUID.Generate()
                });
            }
            provider.SetSpriteRects(rects.ToArray());
            ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }
        finally { Object.DestroyImmediate(texture); }
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetUILayer(Transform root)
    {
        root.gameObject.layer = 5;
        foreach (Transform child in root)
            SetUILayer(child);
    }
}
