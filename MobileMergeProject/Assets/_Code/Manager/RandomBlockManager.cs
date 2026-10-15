using System.Collections;
using System.Collections.Generic;
using _Code.Block;
using _Code.Field;
using UnityEngine;

namespace _Code.Manager
{
    public class RandomBlockManager : MonoBehaviour
    {
        private const float FeaturedCatChance = 0.4f;

        private static readonly string[][] TutorialShapeSets =
        {
            new[] { "Two Horizontal", "Two Horizontal", "Two Horizontal" },
            new[] { "S", "Two Horizontal", "Single" }
        };

        [SerializeField] private BlockPiece[] _pieces;

        private readonly List<BlockShape> _installableShapes = new List<BlockShape>();
        private bool _tutorialMode;
        private int _tutorialSetIndex;

        private readonly Color[] _colors =
        {
            new Color(0.98f, 0.35f, 0.33f),
            new Color(1f, 0.73f, 0.25f),
            new Color(0.27f, 0.72f, 1f),
            new Color(0.35f, 0.88f, 0.54f),
            new Color(0.74f, 0.48f, 1f),
            new Color(1f, 0.46f, 0.76f)
        };

        public IReadOnlyList<BlockPiece> Pieces => _pieces;

        public void SetTutorialMode(bool enabled)
        {
            _tutorialMode = enabled;
            _tutorialSetIndex = 0;
        }

        public void Initialize(BlockPiece[] pieces)
        {
            if (pieces != null && pieces.Length > 0)
                _pieces = pieces;

            if (_pieces == null)
                return;

            foreach (BlockPiece piece in _pieces)
                piece.CaptureSlotPosition();
        }

        public bool GiveNewSet(BlockField blockField)
        {
            if (_pieces == null || blockField == null)
                return false;

            if (_tutorialMode && _tutorialSetIndex < TutorialShapeSets.Length)
            {
                GiveTutorialSet(TutorialShapeSets[_tutorialSetIndex]);
                _tutorialSetIndex++;
                return true;
            }

            _tutorialMode = false;

            CacheInstallableShapes(blockField);

            if (_installableShapes.Count == 0)
                return false;

            Sprite featuredCatSprite = blockField.GetMostCommonCatSprite() ??
                                       BlockBlastSpriteLibrary.GetRandomCatBlockSprite();
            foreach (BlockPiece piece in _pieces)
            {
                BlockShape shape = _installableShapes[Random.Range(0, _installableShapes.Count)];
                Color color = _colors[Random.Range(0, _colors.Length)];
                Sprite catSprite = Random.value < FeaturedCatChance
                    ? featuredCatSprite
                    : BlockBlastSpriteLibrary.GetRandomCatBlockSprite();

                piece.Configure(shape, color, catSprite);
            }

            return true;
        }

        private void GiveTutorialSet(IReadOnlyList<string> shapeNames)
        {
            Sprite tutorialCatSprite = BlockBlastSpriteLibrary.CatBlockSprite;

            for (int i = 0; i < _pieces.Length; i++)
            {
                string shapeName = i < shapeNames.Count
                    ? shapeNames[i]
                    : "Single";
                BlockShape shape = FindShape(shapeName) ?? FindShape("Single");
                _pieces[i].Configure(shape, Color.white, tutorialCatSprite);
            }
        }

        private static BlockShape FindShape(string shapeName)
        {
            foreach (BlockShape shape in BlockShapeCatalog.Shapes)
            {
                if (shape.Name == shapeName)
                    return shape;
            }

            return null;
        }

        public bool AreAllPiecesPlaced()
        {
            if (_pieces == null || _pieces.Length == 0)
                return false;

            foreach (BlockPiece piece in _pieces)
            {
                if (!piece.IsPlaced)
                    return false;
            }

            return true;
        }

        public bool HasAnyAvailablePlacement(BlockField blockField)
        {
            if (_pieces == null || blockField == null)
                return false;

            foreach (BlockPiece piece in _pieces)
            {
                if (piece == null)
                    continue;

                if (!piece.IsPlaced && blockField.HasAnyPlacement(piece))
                    return true;
            }

            return false;
        }

        public void HighlightUnplaceablePieces(BlockField blockField)
        {
            if (_pieces == null)
                return;

            foreach (BlockPiece piece in _pieces)
            {
                if (piece == null || piece.IsPlaced)
                    continue;

                bool canPlace = blockField != null && blockField.HasAnyPlacement(piece);
                piece.SetPlacementBlockedHighlight(!canPlace);
            }
        }

        public void ClearPlacementHighlights()
        {
            if (_pieces == null)
                return;

            foreach (BlockPiece piece in _pieces)
            {
                if (piece != null)
                    piece.SetPlacementBlockedHighlight(false);
            }
        }

        public IEnumerator PlayUnplaceablePiecesAnimation(BlockField blockField)
        {
            if (_pieces == null || blockField == null)
                yield break;

            List<BlockPiece> unplaceablePieces = new List<BlockPiece>(_pieces.Length);
            foreach (BlockPiece piece in _pieces)
            {
                if (piece != null && !piece.IsPlaced && !blockField.HasAnyPlacement(piece))
                    unplaceablePieces.Add(piece);
            }

            if (unplaceablePieces.Count == 0)
                yield break;

            Vector2Int centerPoint = new Vector2Int(blockField.Width / 2, blockField.Height / 2);
            Vector3 centerPosition = blockField.GetWorldPosition(centerPoint);
            const float staggerDelay = 0.07f;

            for (int i = 0; i < unplaceablePieces.Count; i++)
            {
                BlockPiece piece = unplaceablePieces[i];
                StartCoroutine(piece.PlayPlacementBlockedAnimation(centerPosition, i * staggerDelay));
            }

            float totalDuration = BlockPiece.PlacementBlockedAnimationDuration +
                                  (unplaceablePieces.Count - 1) * staggerDelay;
            yield return new WaitForSecondsRealtime(totalDuration);
        }

        public bool CanPlaceAllRemainingPieces(BlockField blockField)
        {
            if (_pieces == null || blockField == null)
                return false;

            foreach (BlockPiece piece in _pieces)
            {
                if (piece == null || piece.IsPlaced)
                    continue;

                if (!blockField.HasAnyPlacement(piece))
                    return false;
            }

            return true;
        }

        public bool HasAnyInstallableShape(BlockField blockField)
        {
            if (blockField == null)
                return false;

            foreach (BlockShape shape in BlockShapeCatalog.Shapes)
            {
                if (blockField.HasAnyPlacement(shape))
                    return true;
            }

            return false;
        }

        private void CacheInstallableShapes(BlockField blockField)
        {
            _installableShapes.Clear();

            foreach (BlockShape shape in BlockShapeCatalog.Shapes)
            {
                if (blockField.HasAnyPlacement(shape))
                    _installableShapes.Add(shape);
            }
        }
    }
}
