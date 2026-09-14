using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SingleCatBlockSpriteBuilder
{
    private const int OutlineThicknessPixels = 4;

    private static readonly SpriteSpec[] SpriteSpecs =
    {
        new SpriteSpec("Assets/Resources/BlockBlast/BlackCatBlockSprite.png", "Assets/Resources/BlockBlast/BlackCatBlockSpriteSingle.png"),
        new SpriteSpec("Assets/Resources/BlockBlast/WhiteCatBlockSprite.png", "Assets/Resources/BlockBlast/WhiteCatBlockSpriteSingle.png"),
        new SpriteSpec("Assets/Resources/BlockBlast/SphynxCatBlockSprite.png", "Assets/Resources/BlockBlast/SphynxCatBlockSpriteSingle.png"),
        new SpriteSpec("Assets/Resources/BlockBlast/CatBlockSprite.png", "Assets/Resources/BlockBlast/CatBlockSpriteSingle.png")
    };

    [MenuItem("Tools/Catch the Cats/Build Single-Cell Cat Sprites")]
    public static void Build()
    {
        foreach (SpriteSpec spec in SpriteSpecs)
            BuildSprite(spec);

        AssetDatabase.SaveAssets();
        Debug.Log("Single-cell cat outline sprites are ready.");
    }

    public static void BuildAndVerify()
    {
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");

        Build();
        CatHomeValidation.Run();
    }

    private static void BuildSprite(SpriteSpec spec)
    {
        TextureImporter sourceImporter = AssetImporter.GetAtPath(spec.SourcePath) as TextureImporter;
        Texture2D sourceTexture = LoadTexture(spec.SourcePath);
        Texture2D outlinedTexture = CreateOutlinedTexture(sourceTexture, OutlineThicknessPixels);

        try
        {
            File.WriteAllBytes(spec.OutputPath, outlinedTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(spec.OutputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(AssetImporter.GetAtPath(spec.OutputPath) as TextureImporter, sourceImporter);
        }
        finally
        {
            Object.DestroyImmediate(sourceTexture);
            Object.DestroyImmediate(outlinedTexture);
        }
    }

    private static Texture2D LoadTexture(string assetPath)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(assetPath)))
        {
            Object.DestroyImmediate(texture);
            throw new InvalidDataException("Unable to read sprite texture: " + assetPath);
        }

        return texture;
    }

    private static Texture2D CreateOutlinedTexture(Texture2D sourceTexture, int outlineThickness)
    {
        int width = sourceTexture.width;
        int height = sourceTexture.height;
        Color32[] sourcePixels = sourceTexture.GetPixels32();
        Color32[] outlinedPixels = new Color32[sourcePixels.Length];
        int squaredRadius = outlineThickness * outlineThickness;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (sourcePixels[y * width + x].a < 16)
                continue;

            for (int offsetY = -outlineThickness; offsetY <= outlineThickness; offsetY++)
            for (int offsetX = -outlineThickness; offsetX <= outlineThickness; offsetX++)
            {
                if (offsetX * offsetX + offsetY * offsetY > squaredRadius)
                    continue;

                int targetX = x + offsetX;
                int targetY = y + offsetY;
                if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height)
                    continue;

                outlinedPixels[targetY * width + targetX] = new Color32(255, 255, 255, 255);
            }
        }

        for (int index = 0; index < sourcePixels.Length; index++)
            outlinedPixels[index] = BlendOverWhite(sourcePixels[index], outlinedPixels[index]);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "SingleCellCatOutline"
        };
        result.SetPixels32(outlinedPixels);
        result.Apply();
        return result;
    }

    private static Color32 BlendOverWhite(Color32 source, Color32 outline)
    {
        if (source.a == 0)
            return outline;

        if (source.a == byte.MaxValue)
            return source;

        float sourceAlpha = source.a / 255f;
        float outlineAlpha = outline.a / 255f;
        float alpha = sourceAlpha + outlineAlpha * (1f - sourceAlpha);
        if (alpha <= 0f)
            return default;

        float inverseAlpha = 1f / alpha;
        byte red = (byte)Mathf.RoundToInt((source.r / 255f * sourceAlpha + outlineAlpha * (1f - sourceAlpha)) * inverseAlpha * 255f);
        byte green = (byte)Mathf.RoundToInt((source.g / 255f * sourceAlpha + outlineAlpha * (1f - sourceAlpha)) * inverseAlpha * 255f);
        byte blue = (byte)Mathf.RoundToInt((source.b / 255f * sourceAlpha + outlineAlpha * (1f - sourceAlpha)) * inverseAlpha * 255f);
        return new Color32(red, green, blue, (byte)Mathf.RoundToInt(alpha * 255f));
    }

    private static void ConfigureImporter(TextureImporter importer, TextureImporter sourceImporter)
    {
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = sourceImporter != null ? sourceImporter.spritePixelsPerUnit : 256f;
        importer.filterMode = sourceImporter != null ? sourceImporter.filterMode : FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private readonly struct SpriteSpec
    {
        public SpriteSpec(string sourcePath, string outputPath)
        {
            SourcePath = sourcePath;
            OutputPath = outputPath;
        }

        public string SourcePath { get; }
        public string OutputPath { get; }
    }
}
