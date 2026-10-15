using System.Collections.Generic;
using _Code.Block;
using _Code.SO;
using Code.Core;
using Code.Core.Events.Bus;
using Code.Core.Events.Bus.TextEvent;
using UnityEngine;

namespace _Code.Manager
{
    public sealed class LineClearFeedbackController : MonoBehaviour
    {
        private const float SwipeClearEffectSizeMultiplier = 1.4f;

        private static readonly Color BlackCatEffectColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color WhiteCatEffectColor = Color.white;
        private static readonly Color YellowCatEffectColor = new Color(1f, 0.72f, 0.62f, 1f);

        [SerializeField] private LineClearEffectPlayer lineClearEffectPlayer;

        public void Configure(LineClearEffectPlayer lineClearEffectPlayer)
        {
            if (this.lineClearEffectPlayer == null)
                this.lineClearEffectPlayer = lineClearEffectPlayer;
        }

        public void PlayPlacementClear(
            BlockPiece sourcePiece,
            int clearedLines,
            IReadOnlyList<Vector3> clearedBlockPositions)
        {
            if (clearedLines <= 0)
                return;

            RaiseClearNotice(clearedLines, false);
            lineClearEffectPlayer?.Play(clearedLines, clearedBlockPositions, GetEffectColor(sourcePiece));
        }

        public void PlaySwipeClear(int clearedLines, IReadOnlyList<Vector3> clearedBlockPositions)
        {
            if (clearedLines <= 0)
                return;

            RaiseClearNotice(clearedLines, true);
            lineClearEffectPlayer?.Play(
                clearedLines,
                clearedBlockPositions,
                Color.white,
                SwipeClearEffectSizeMultiplier);
        }

        private static void RaiseClearNotice(int clearedLines, bool isSwipeClear)
        {
            switch (clearedLines)
            {
                case 1:
                    Bus<CamShakeEvent>.Raise(new CamShakeEvent(0.1f));
                    SoundManager.Instance.PlayClip("ClearSound");
                    break;
                case 2:
                    Bus<CamShakeEvent>.Raise(new CamShakeEvent(0.2f));
                    SoundManager.Instance.PlayClip("ClearSound");
                    break;
                case 3:
                    Bus<CamShakeEvent>.Raise(new CamShakeEvent(0.3f));
                    SoundManager.Instance.PlayClip("ClearSound");
                    break;
                case 4:
                    Bus<CamShakeEvent>.Raise(new CamShakeEvent(0.4f));
                    SoundManager.Instance.PlayClip("ClearSound");
                    break;
            }

            if (isSwipeClear)
            {
                Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.PointX2));
                return;
            }

            switch (clearedLines)
            {
                case 1:
                    Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.Clear));
                    break;
                case 2:
                    Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.Double));
                    break;
                case 3:
                    Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.Tripple));
                    break;
                case 4:
                    Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.Quadra));
                    break;
            }
        }

        private static Color GetEffectColor(BlockPiece piece)
        {
            if (piece == null || piece.BlockSprite == null)
                return WhiteCatEffectColor;

            Sprite blockSprite = piece.BlockSprite;
            IReadOnlyList<Sprite> catSprites = BlockBlastSpriteLibrary.CatBlockSprites;

            if (catSprites.Count > 0 && blockSprite == catSprites[0])
                return BlackCatEffectColor;

            if (catSprites.Count > 1 && blockSprite == catSprites[1])
                return WhiteCatEffectColor;

            if (catSprites.Count > 2 && blockSprite == catSprites[2])
                return YellowCatEffectColor;

            return GetEffectColorByName(blockSprite.name);
        }

        private static Color GetEffectColorByName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return WhiteCatEffectColor;

            if (spriteName.Contains("Black"))
                return BlackCatEffectColor;

            if (spriteName.Contains("White"))
                return WhiteCatEffectColor;

            if (spriteName.Contains("Sphynx") || spriteName.Contains("CatBlock"))
                return YellowCatEffectColor;

            return WhiteCatEffectColor;
        }
    }
}
