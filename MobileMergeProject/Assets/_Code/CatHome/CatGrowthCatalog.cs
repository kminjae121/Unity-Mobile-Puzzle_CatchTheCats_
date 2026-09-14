using System;
using UnityEngine;

namespace _Code.CatHome
{
    // Values are save-file keys. Keep their order stable.
    public enum HomeCatType { Black, White, Sphynx, Orange }

    [CreateAssetMenu(menuName = "Catch the Cats/Cat Growth Catalog")]
    public sealed class CatGrowthCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class CatDefinition
        {
            public HomeCatType type;
            public Sprite blockSprite;
            public Sprite kitten;
            public Sprite juvenile;
            public Sprite adult;
            [Tooltip("Bottom-center position in the background image (0..1).")]
            public Vector2 homePosition;
            [Tooltip("Maximum width and height relative to the background image.")]
            public Vector2 adultSize;

            public Sprite GetSprite(int stage) => stage >= 2 ? adult : stage == 1 ? juvenile : kitten;
        }

        [Min(1)] public int juvenileLineCount = 1;
        [Min(2)] public int adultLineCount = 3;
        public CatDefinition[] cats = Array.Empty<CatDefinition>();

        public int GetStage(int lineCount)
        {
            if (lineCount >= Mathf.Max(juvenileLineCount + 1, adultLineCount))
                return 2;
            return lineCount >= Mathf.Max(1, juvenileLineCount) ? 1 : 0;
        }

        public float GetSize(int lineCount)
        {
            int juvenileAt = Mathf.Max(1, juvenileLineCount);
            int adultAt = Mathf.Max(juvenileAt + 1, adultLineCount);
            return lineCount < juvenileAt
                ? Mathf.Lerp(0.62f, 0.8f, Mathf.Clamp01((float)lineCount / juvenileAt))
                : Mathf.Lerp(0.8f, 1f, Mathf.Clamp01((float)(lineCount - juvenileAt) / (adultAt - juvenileAt)));
        }

        public CatDefinition Find(Sprite blockSprite)
        {
            if (blockSprite == null)
                return null;
            foreach (CatDefinition cat in cats)
            {
                if (cat != null && cat.blockSprite == blockSprite)
                    return cat;
            }
            return null;
        }
    }
}
