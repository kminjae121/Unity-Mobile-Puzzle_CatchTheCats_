using _Code.Block;
using _Code.Field;
using _Code.Stage;
using System.Collections.Generic;
using UnityEngine;

namespace _Code.Manager
{
    public sealed class StageModeController : MonoBehaviour
    {
        private const string ScoreSuffix = "\uC810";
        private const string StageLabel = "\uC2A4\uD14C\uC774\uC9C0";
        private const string GoalLabel = "\uBAA9\uD45C";
        private const string CheeseLabel = "\uCE58\uC988";
        private const string PlacementLabel = "\uBC30\uCE58";
        private const string RemoveLabel = "\uC81C\uAC70";
        private const string BlackCatLabel = "\uAC80\uC740 \uACE0\uC591\uC774";
        private const string WhiteCatLabel = "\uD770 \uACE0\uC591\uC774";
        private const string SphynxCatLabel = "\uC2A4\uD551\uD06C\uC2A4 \uACE0\uC591\uC774";
        private const string OrangeCatLabel = "\uC8FC\uD669 \uACE0\uC591\uC774";
        private const string CatLabel = "\uACE0\uC591\uC774";

        private StageDefinition _stageDefinition;
        private readonly HashSet<Vector2Int> _remainingCheeseCells = new HashSet<Vector2Int>();
        private int _usedPlacementCount;
        private int _removedTargetCatCount;

        public bool IsStageMode { get; private set; }
        public int StageNumber => IsStageMode ? _stageDefinition.Number : 0;
        public StageGoalType GoalType => IsStageMode ? _stageDefinition.GoalType : StageGoalType.Score;
        public bool HasScoreGoal => IsStageMode && _stageDefinition.GoalType == StageGoalType.Score;
        public int TargetScore => HasScoreGoal ? _stageDefinition.TargetScore : 0;
        public int RemainingCheeseCount => _remainingCheeseCells.Count;
        public int TargetCheeseCount => IsStageMode ? _stageDefinition.TargetCheeseCount : 0;
        public int TargetCatRemovalCount => IsStageMode ? _stageDefinition.TargetCatRemovalCount : 0;
        public int RemovedTargetCatCount => _removedTargetCatCount;
        public int UsedPlacementCount => _usedPlacementCount;
        public int MaxPlacementCount => IsStageMode ? _stageDefinition.MaxPlacementCount : 0;
        public int RemainingPlacementCount => MaxPlacementCount > 0 ? Mathf.Max(0, MaxPlacementCount - _usedPlacementCount) : 0;
        public bool IsFailedByPlacementLimit =>
            IsStageMode &&
            _stageDefinition.GoalType == StageGoalType.Cheese &&
            MaxPlacementCount > 0 &&
            _usedPlacementCount >= MaxPlacementCount &&
            _remainingCheeseCells.Count > 0;
        public string StageSelectSceneName => "StageScene";

        public void Initialize(BlockField blockField, GameObject owner)
        {
            IsStageMode = StageRunContext.TryGetSelectedStage(out _stageDefinition);
            _remainingCheeseCells.Clear();
            _usedPlacementCount = 0;
            _removedTargetCatCount = 0;

            if (!IsStageMode || blockField == null)
                return;

            int groupId = 10000 + _stageDefinition.Number * 100;

            foreach (Vector2Int point in _stageDefinition.StartingCells)
            {
                if (blockField.TryGetField(point, out _Code.Field.Field field))
                    field.SetObject(owner, Color.white, BlockBlastSpriteLibrary.GetRandomCatBlockSprite(), groupId++);
            }

            foreach (Vector2Int point in _stageDefinition.CheeseCells)
            {
                if (!blockField.TryGetField(point, out _Code.Field.Field field))
                    continue;

                field.SetStageCheeseObject(owner, BlockBlastSpriteLibrary.CheeseBlockSprite, groupId++);
                _remainingCheeseCells.Add(point);
            }
        }

        public void NotifyClearedPoints(IEnumerable<Vector2Int> clearedPoints)
        {
            if (!IsStageMode || _stageDefinition.GoalType != StageGoalType.Cheese || clearedPoints == null)
                return;

            foreach (Vector2Int point in clearedPoints)
                _remainingCheeseCells.Remove(point);
        }

        public void NotifyClearedCatSprites(IEnumerable<Sprite> clearedSprites)
        {
            if (!IsStageMode || _stageDefinition.GoalType != StageGoalType.CatRemoval || clearedSprites == null)
                return;

            foreach (Sprite sprite in clearedSprites)
            {
                if (IsTargetCatSprite(sprite))
                    _removedTargetCatCount++;
            }
        }

        public void NotifyPiecePlaced()
        {
            if (!IsStageMode || _stageDefinition.GoalType != StageGoalType.Cheese)
                return;

            _usedPlacementCount++;
        }

        public void MarkCleared()
        {
            if (IsStageMode)
                StageProgress.MarkCleared(_stageDefinition.Number);
        }

        public string GetNextSceneNameAfterClear()
        {
            if (!IsStageMode)
                return StageSelectSceneName;

            int nextStageNumber = _stageDefinition.Number + 1;
            return nextStageNumber <= StageCatalog.MaxStage
                ? StageCatalog.GetStageSceneName(nextStageNumber)
                : StageSelectSceneName;
        }

        public bool IsComplete(int score)
        {
            if (!IsStageMode)
                return false;

            if (_stageDefinition.GoalType == StageGoalType.Cheese)
                return _remainingCheeseCells.Count == 0;

            if (_stageDefinition.GoalType == StageGoalType.CatRemoval)
                return TargetCatRemovalCount > 0 && _removedTargetCatCount >= TargetCatRemovalCount;

            return score >= _stageDefinition.TargetScore;
        }

        public string GetStartMessage()
        {
            if (!IsStageMode)
                return string.Empty;

            if (_stageDefinition.GoalType == StageGoalType.Cheese)
                return $"{StageLabel} {_stageDefinition.Number}\n{GoalLabel} : {CheeseLabel} {TargetCheeseCount - RemainingCheeseCount}/{TargetCheeseCount} {RemoveLabel}\n{PlacementLabel} : {RemainingPlacementCount}";

            if (_stageDefinition.GoalType == StageGoalType.CatRemoval)
                return $"{StageLabel} {_stageDefinition.Number}\n{GoalLabel} : {GetTargetCatLabel()} {_removedTargetCatCount}/{TargetCatRemovalCount} {RemoveLabel}";

            return $"{StageLabel} {_stageDefinition.Number}\n{GoalLabel} : {_stageDefinition.TargetScore}{ScoreSuffix} \uB2EC\uC131";
        }

        public string GetClearMessage()
        {
            if (!IsStageMode)
                return string.Empty;

            return $"{StageLabel} {_stageDefinition.Number} \uD074\uB9AC\uC5B4!";
        }

        private bool IsTargetCatSprite(Sprite sprite)
        {
            if (sprite == null)
                return false;

            switch (_stageDefinition.TargetCatType)
            {
                case StageTargetCatType.Black:
                    return IsCatSprite(sprite, 0, "Black");
                case StageTargetCatType.White:
                    return IsCatSprite(sprite, 1, "White");
                case StageTargetCatType.Sphynx:
                    return IsCatSprite(sprite, 2, "Sphynx");
                case StageTargetCatType.Orange:
                    return IsCatSprite(sprite, 3, "CatBlock") || ContainsSpriteName(sprite, "Orange");
                default:
                    return false;
            }
        }

        private string GetTargetCatLabel()
        {
            switch (_stageDefinition.TargetCatType)
            {
                case StageTargetCatType.Black:
                    return BlackCatLabel;
                case StageTargetCatType.White:
                    return WhiteCatLabel;
                case StageTargetCatType.Sphynx:
                    return SphynxCatLabel;
                case StageTargetCatType.Orange:
                    return OrangeCatLabel;
                default:
                    return CatLabel;
            }
        }

        private static bool IsCatSprite(Sprite sprite, int spriteIndex, string nameToken)
        {
            IReadOnlyList<Sprite> catSprites = BlockBlastSpriteLibrary.CatBlockSprites;

            if (spriteIndex >= 0 && spriteIndex < catSprites.Count && sprite == catSprites[spriteIndex])
                return true;

            return ContainsSpriteName(sprite, nameToken);
        }

        private static bool ContainsSpriteName(Sprite sprite, string nameToken)
        {
            return sprite != null &&
                   !string.IsNullOrEmpty(sprite.name) &&
                   sprite.name.Contains(nameToken);
        }
    }
}
