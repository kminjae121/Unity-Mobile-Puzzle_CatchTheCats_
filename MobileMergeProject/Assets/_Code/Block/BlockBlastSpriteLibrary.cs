using System.Collections.Generic;
using UnityEngine;

namespace _Code.Block
{
    public static class BlockBlastSpriteLibrary
    {
        private const string CatBlockSpritePath = "BlockBlast/CatBlockSprite";
        private const string BlackCatBlockSpritePath = "BlockBlast/BlackCatBlockSprite";
        private const string WhiteCatBlockSpritePath = "BlockBlast/WhiteCatBlockSprite";
        private const string SphynxCatBlockSpritePath = "BlockBlast/SphynxCatBlockSprite";
        private const string BlackCatBlockSingleSpritePath = "BlockBlast/BlackCatBlockSpriteSingle";
        private const string WhiteCatBlockSingleSpritePath = "BlockBlast/WhiteCatBlockSpriteSingle";
        private const string SphynxCatBlockSingleSpritePath = "BlockBlast/SphynxCatBlockSpriteSingle";
        private const string CatBlockSingleSpritePath = "BlockBlast/CatBlockSpriteSingle";
        private const string CheeseBlockSpritePath = "BlockBlast/CheeseBlockSprite";
        private const string MouseSpritePath = "BlockBlast/MouseSprite";
        private const string MouseIdleSpritePathPrefix = "BlockBlast/MouseIdle_";
        private const string MouseMoveRightSpritePathPrefix = "BlockBlast/MouseMoveRight_";
        private const string MouseMoveLeftSpritePathPrefix = "BlockBlast/MouseMoveLeft_";
        private const string MouseMoveUpSpritePathPrefix = "BlockBlast/MouseMoveUp_";
        private const string MouseMoveDownSpritePathPrefix = "BlockBlast/MouseMoveDown_";
        private const string CleanCatHomeBackgroundSpritePath = "BlockBlast/CleanCatHomeBackground";
        private const string NightCatHomeBackgroundSpritePath = "BlockBlast/NightCatHomeBackground";
        private const string CatTowerFrameSpritePath = "BlockBlast/CatTowerFrameSprite";
        private const string MouseHoleFrameSpritePath = "BlockBlast/MouseHoleFrameSprite";

        private static Sprite _catBlockSprite;
        private static Sprite _blackCatBlockSprite;
        private static Sprite _whiteCatBlockSprite;
        private static Sprite _sphynxCatBlockSprite;
        private static Sprite _blackCatBlockSingleSprite;
        private static Sprite _whiteCatBlockSingleSprite;
        private static Sprite _sphynxCatBlockSingleSprite;
        private static Sprite _catBlockSingleSprite;
        private static Sprite _cheeseBlockSprite;
        private static Sprite[] _catBlockSprites;
        private static Sprite _mouseSprite;
        private static Sprite[] _mouseIdleSprites;
        private static Sprite[] _mouseMoveRightSprites;
        private static Sprite[] _mouseMoveLeftSprites;
        private static Sprite[] _mouseMoveUpSprites;
        private static Sprite[] _mouseMoveDownSprites;
        private static Sprite _cleanCatHomeBackgroundSprite;
        private static Sprite _nightCatHomeBackgroundSprite;
        private static Sprite _catTowerFrameSprite;
        private static Sprite _mouseHoleFrameSprite;

        public static Sprite CatBlockSprite => LoadSprite(CatBlockSpritePath, ref _catBlockSprite) ?? GetFirstCatBlockSprite();
        public static Sprite CheeseBlockSprite => LoadSprite(CheeseBlockSpritePath, ref _cheeseBlockSprite) ?? CatBlockSprite;
        public static Sprite[] CatBlockSprites => GetCatBlockSprites();
        public static Sprite MouseSprite => LoadSprite(MouseSpritePath, ref _mouseSprite);
        public static Sprite[] MouseIdleSprites => LoadSpriteSequence(MouseIdleSpritePathPrefix, ref _mouseIdleSprites);
        public static Sprite CleanCatHomeBackgroundSprite => LoadSprite(CleanCatHomeBackgroundSpritePath, ref _cleanCatHomeBackgroundSprite);
        public static Sprite NightCatHomeBackgroundSprite => LoadSprite(NightCatHomeBackgroundSpritePath, ref _nightCatHomeBackgroundSprite) ?? CleanCatHomeBackgroundSprite;
        public static Sprite CatTowerFrameSprite => LoadSprite(CatTowerFrameSpritePath, ref _catTowerFrameSprite);
        public static Sprite MouseHoleFrameSprite => LoadSprite(MouseHoleFrameSpritePath, ref _mouseHoleFrameSprite) ?? CatTowerFrameSprite;

        public static Sprite GetRandomCatBlockSprite()
        {
            Sprite[] sprites = CatBlockSprites;
            return sprites.Length > 0 ? sprites[Random.Range(0, sprites.Length)] : null;
        }

        public static Sprite GetSingleCellCatSprite(Sprite sprite)
        {
            if (sprite == null)
                return null;

            if (sprite == LoadSprite(BlackCatBlockSpritePath, ref _blackCatBlockSprite))
                return LoadSprite(BlackCatBlockSingleSpritePath, ref _blackCatBlockSingleSprite) ?? sprite;

            if (sprite == LoadSprite(WhiteCatBlockSpritePath, ref _whiteCatBlockSprite))
                return LoadSprite(WhiteCatBlockSingleSpritePath, ref _whiteCatBlockSingleSprite) ?? sprite;

            if (sprite == LoadSprite(SphynxCatBlockSpritePath, ref _sphynxCatBlockSprite))
                return LoadSprite(SphynxCatBlockSingleSpritePath, ref _sphynxCatBlockSingleSprite) ?? sprite;

            if (sprite == LoadSprite(CatBlockSpritePath, ref _catBlockSprite))
                return LoadSprite(CatBlockSingleSpritePath, ref _catBlockSingleSprite) ?? sprite;

            return sprite;
        }

        public static Sprite[] GetMouseMoveSprites(Vector2Int direction)
        {
            direction = NormalizeMouseDirection(direction);

            if (direction == Vector2Int.right)
                return LoadSpriteSequence(MouseMoveRightSpritePathPrefix, ref _mouseMoveRightSprites);

            if (direction == Vector2Int.left)
                return LoadSpriteSequence(MouseMoveLeftSpritePathPrefix, ref _mouseMoveLeftSprites);

            if (direction == Vector2Int.up)
                return LoadSpriteSequence(MouseMoveUpSpritePathPrefix, ref _mouseMoveUpSprites);

            return LoadSpriteSequence(MouseMoveDownSpritePathPrefix, ref _mouseMoveDownSprites);
        }

        private static Sprite[] GetCatBlockSprites()
        {
            if (_catBlockSprites != null)
                return _catBlockSprites;

            List<Sprite> sprites = new List<Sprite>
            {
                LoadSprite(BlackCatBlockSpritePath, ref _blackCatBlockSprite),
                LoadSprite(WhiteCatBlockSpritePath, ref _whiteCatBlockSprite),
                LoadSprite(SphynxCatBlockSpritePath, ref _sphynxCatBlockSprite),
                LoadSprite(CatBlockSpritePath, ref _catBlockSprite)
            };

            sprites.RemoveAll(sprite => sprite == null);

            if (sprites.Count == 0)
            {
                Sprite fallback = LoadSprite(CatBlockSpritePath, ref _catBlockSprite);

                if (fallback != null)
                    sprites.Add(fallback);
            }

            _catBlockSprites = sprites.ToArray();
            return _catBlockSprites;
        }

        private static Sprite GetFirstCatBlockSprite()
        {
            Sprite[] sprites = CatBlockSprites;
            return sprites.Length > 0 ? sprites[0] : null;
        }

        private static Sprite LoadSprite(string path, ref Sprite sprite)
        {
            if (sprite == null)
                sprite = Resources.Load<Sprite>(path);

            return sprite;
        }

        private static Sprite[] LoadSpriteSequence(string pathPrefix, ref Sprite[] sprites)
        {
            if (sprites != null)
                return sprites;

            List<Sprite> loadedSprites = new List<Sprite>();

            for (int i = 0; i < 4; i++)
            {
                Sprite sprite = Resources.Load<Sprite>($"{pathPrefix}{i}");

                if (sprite != null)
                    loadedSprites.Add(sprite);
            }

            if (loadedSprites.Count == 0 && MouseSprite != null)
                loadedSprites.Add(MouseSprite);

            sprites = loadedSprites.ToArray();
            return sprites;
        }

        private static Vector2Int NormalizeMouseDirection(Vector2Int direction)
        {
            if (direction == Vector2Int.zero)
                return Vector2Int.down;

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                return direction.x > 0 ? Vector2Int.right : Vector2Int.left;

            return direction.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }
}
