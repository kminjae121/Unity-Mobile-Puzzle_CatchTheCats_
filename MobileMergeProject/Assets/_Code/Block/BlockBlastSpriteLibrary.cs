using System.Collections.Generic;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace _Code.Block
{
    public static class BlockBlastSpriteLibrary
    {
        private const string CatBlockSpritePath = "Sprite/Cat";
        private const string BlackCatBlockSpritePath = "Sprite/BlackCat";
        private const string WhiteCatBlockSpritePath = "Sprite/WhiteCat";
        private const string SphynxCatBlockSpritePath = "Sprite/SphynxCat";
        private const string BlackCatBlockSingleSpritePath = "Sprite/BlackCatSingle";
        private const string WhiteCatBlockSingleSpritePath = "Sprite/WhiteCatSingle";
        private const string SphynxCatBlockSingleSpritePath = "Sprite/SphyncSingle";
        private const string CatBlockSingleSpritePath = "Sprite/CatSingle";
        private const string CheeseBlockSpritePath = "Sprite/Cheese";
        private const string MouseSpritePath = "Mouse/MouseSprite";
        private const string MouseIdleSpritePathPrefix = "Mouse/MouseIdle_";
        private const string MouseMoveRightSpritePathPrefix = "Mouse/MouseMoveRight_";
        private const string MouseMoveLeftSpritePathPrefix = "Mouse/MouseMoveLeft_";
        private const string MouseMoveUpSpritePathPrefix = "Mouse/MouseMoveUp_";
        private const string MouseMoveDownSpritePathPrefix = "Mouse/MouseMoveDown_";
        private const string CleanCatHomeBackgroundSpritePath = "BackGround/CleanHomeBackGround";
        private const string NightCatHomeBackgroundSpritePath = "BackGround/NightCatHome";
        private const string CatTowerFrameSpritePath = "Sprite/TowerFrame";
        private const string MouseHoleFrameSpritePath = "Sprite/MouseHole";

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
        private static Task _initializationTask;

        public static Sprite CatBlockSprite => LoadAddressableSprite(CatBlockSpritePath, ref _catBlockSprite) ?? GetFirstCatBlockSprite();
        public static Sprite CheeseBlockSprite => LoadAddressableSprite(CheeseBlockSpritePath, ref _cheeseBlockSprite) ?? CatBlockSprite;
        public static Sprite[] CatBlockSprites => GetCatBlockSprites();
        public static Sprite MouseSprite => LoadAddressableSprite(MouseSpritePath, ref _mouseSprite);
        public static Sprite[] MouseIdleSprites => LoadSpriteSequence(MouseIdleSpritePathPrefix, ref _mouseIdleSprites);
        public static Sprite CleanCatHomeBackgroundSprite => LoadAddressableSprite(CleanCatHomeBackgroundSpritePath, ref _cleanCatHomeBackgroundSprite);
        public static Sprite NightCatHomeBackgroundSprite => LoadAddressableSprite(NightCatHomeBackgroundSpritePath, ref _nightCatHomeBackgroundSprite) ?? CleanCatHomeBackgroundSprite;
        public static Sprite CatTowerFrameSprite => LoadAddressableSprite(CatTowerFrameSpritePath, ref _catTowerFrameSprite);
        public static Sprite MouseHoleFrameSprite => LoadAddressableSprite(MouseHoleFrameSpritePath, ref _mouseHoleFrameSprite) ?? CatTowerFrameSprite;

        public static Task InitializeAsync()
        {
            return _initializationTask ??= InitializeAddressableSpritesAsync();
        }

        public static Sprite GetRandomCatBlockSprite()
        {
            Sprite[] sprites = CatBlockSprites;
            return sprites.Length > 0 ? sprites[Random.Range(0, sprites.Length)] : null;
        }

        public static Sprite GetSingleCellCatSprite(Sprite sprite)
        {
            if (sprite == null)
                return null;

            if (sprite == LoadAddressableSprite(BlackCatBlockSpritePath, ref _blackCatBlockSprite))
                return LoadAddressableSprite(BlackCatBlockSingleSpritePath, ref _blackCatBlockSingleSprite) ?? sprite;

            if (sprite == LoadAddressableSprite(WhiteCatBlockSpritePath, ref _whiteCatBlockSprite))
                return LoadAddressableSprite(WhiteCatBlockSingleSpritePath, ref _whiteCatBlockSingleSprite) ?? sprite;

            if (sprite == LoadAddressableSprite(SphynxCatBlockSpritePath, ref _sphynxCatBlockSprite))
                return LoadAddressableSprite(SphynxCatBlockSingleSpritePath, ref _sphynxCatBlockSingleSprite) ?? sprite;

            if (sprite == LoadAddressableSprite(CatBlockSpritePath, ref _catBlockSprite))
                return LoadAddressableSprite(CatBlockSingleSpritePath, ref _catBlockSingleSprite) ?? sprite;

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
                LoadAddressableSprite(BlackCatBlockSpritePath, ref _blackCatBlockSprite),
                LoadAddressableSprite(WhiteCatBlockSpritePath, ref _whiteCatBlockSprite),
                LoadAddressableSprite(SphynxCatBlockSpritePath, ref _sphynxCatBlockSprite),
                LoadAddressableSprite(CatBlockSpritePath, ref _catBlockSprite)
            };

            sprites.RemoveAll(sprite => sprite == null);

            if (sprites.Count == 0)
            {
                Sprite fallback = LoadAddressableSprite(CatBlockSpritePath, ref _catBlockSprite);

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

        private static async Task InitializeAddressableSpritesAsync()
        {
            _catBlockSprite = await LoadSprite(CatBlockSpritePath);
            _blackCatBlockSprite = await LoadSprite(BlackCatBlockSpritePath);
            _whiteCatBlockSprite = await LoadSprite(WhiteCatBlockSpritePath);
            _sphynxCatBlockSprite = await LoadSprite(SphynxCatBlockSpritePath);
            _blackCatBlockSingleSprite = await LoadSprite(BlackCatBlockSingleSpritePath);
            _whiteCatBlockSingleSprite = await LoadSprite(WhiteCatBlockSingleSpritePath);
            _sphynxCatBlockSingleSprite = await LoadSprite(SphynxCatBlockSingleSpritePath);
            _catBlockSingleSprite = await LoadSprite(CatBlockSingleSpritePath);
            _cheeseBlockSprite = await LoadSprite(CheeseBlockSpritePath);
            _mouseSprite = await LoadSprite(MouseSpritePath);
            _mouseIdleSprites = await LoadSpriteSequenceAsync(MouseIdleSpritePathPrefix);
            _mouseMoveRightSprites = await LoadSpriteSequenceAsync(MouseMoveRightSpritePathPrefix);
            _mouseMoveLeftSprites = await LoadSpriteSequenceAsync(MouseMoveLeftSpritePathPrefix);
            _mouseMoveUpSprites = await LoadSpriteSequenceAsync(MouseMoveUpSpritePathPrefix);
            _mouseMoveDownSprites = await LoadSpriteSequenceAsync(MouseMoveDownSpritePathPrefix);
            _cleanCatHomeBackgroundSprite = await LoadSprite(CleanCatHomeBackgroundSpritePath);
            _nightCatHomeBackgroundSprite = await LoadSprite(NightCatHomeBackgroundSpritePath);
            _catTowerFrameSprite = await LoadSprite(CatTowerFrameSpritePath);
            _mouseHoleFrameSprite = await LoadSprite(MouseHoleFrameSpritePath);
            _catBlockSprites = null;
        }

        private static async Task<Sprite> LoadSprite(string path)
        {
            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(path);

            return await handle.Task;
        }

        private static Sprite LoadAddressableSprite(string path, ref Sprite sprite)
        {
            if (sprite != null)
                return sprite;

            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(path);
            sprite = handle.WaitForCompletion();
            return sprite;
        }

        private static Sprite[] LoadSpriteSequence(string pathPrefix, ref Sprite[] sprites)
        {
            if (sprites != null)
                return sprites;

            sprites = new Sprite[4];

            for (int i = 0; i < 4; i++)
                sprites[i] = LoadAddressableSprite($"{pathPrefix}{i}", ref sprites[i]);

            return sprites;
        }

        private static async Task<Sprite[]> LoadSpriteSequenceAsync(string pathPrefix)
        {
            Sprite[] sprites = new Sprite[4];

            for (int i = 0; i < sprites.Length; i++)
                sprites[i] = await LoadSprite($"{pathPrefix}{i}");

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
