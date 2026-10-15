using System.Collections.Generic;
using _Code.Block;
using _Code.CatHome;
using _Code.Field;
using UnityEngine;

namespace _Code.Manager
{
    public sealed class GameplayTurnController : MonoBehaviour
    {
        private const int SwipeClearScoreMultiplier = 2;

        [SerializeField] private BlockField blockField;
        [SerializeField] private RandomBlockManager randomBlockManager;
        [SerializeField] private BlockPiece[] pieces;
        [SerializeField] private BlockPlacementPreview placementPreview;
        [SerializeField] private PlayerProgressController playerProgressController;
        [SerializeField] private StageModeController stageModeController;
        [SerializeField] private BoardShiftController boardShiftController;
        [SerializeField] private PlacementScoreGuard placementScoreGuard;
        [SerializeField] private TutorialController tutorialController;
        [SerializeField] private CatHomeGrowthController catHomeGrowthController;
        [SerializeField] private LineClearFeedbackController lineClearFeedbackController;
        [SerializeField] private GameSessionController gameSessionController;

        private readonly List<Vector3> _clearedBlockPositions = new List<Vector3>(36);
        private readonly List<Vector2Int> _clearedBlockPoints = new List<Vector2Int>(36);
        private readonly List<Sprite> _clearedBlockSprites = new List<Sprite>(36);
        private bool _isSubscribed;

        public void Configure(
            BlockField blockField,
            RandomBlockManager randomBlockManager,
            BlockPiece[] pieces,
            BlockPlacementPreview placementPreview,
            PlayerProgressController playerProgressController,
            StageModeController stageModeController,
            BoardShiftController boardShiftController,
            PlacementScoreGuard placementScoreGuard,
            TutorialController tutorialController,
            CatHomeGrowthController catHomeGrowthController,
            LineClearFeedbackController lineClearFeedbackController,
            GameSessionController gameSessionController)
        {
            if (this.blockField == null)
                this.blockField = blockField;

            if (this.randomBlockManager == null)
                this.randomBlockManager = randomBlockManager;

            if (this.pieces == null || this.pieces.Length == 0)
                this.pieces = pieces;

            if (this.placementPreview == null)
                this.placementPreview = placementPreview;

            if (this.playerProgressController == null)
                this.playerProgressController = playerProgressController;

            if (this.stageModeController == null)
                this.stageModeController = stageModeController;

            if (this.boardShiftController == null)
                this.boardShiftController = boardShiftController;

            if (this.placementScoreGuard == null)
                this.placementScoreGuard = placementScoreGuard;

            if (this.tutorialController == null)
                this.tutorialController = tutorialController;

            if (this.catHomeGrowthController == null)
                this.catHomeGrowthController = catHomeGrowthController;

            if (this.lineClearFeedbackController == null)
                this.lineClearFeedbackController = lineClearFeedbackController;

            if (this.gameSessionController == null)
                this.gameSessionController = gameSessionController;
        }

        public void InitializePieces()
        {
            UnsubscribePieces();

            if (pieces == null)
                return;

            foreach (BlockPiece piece in pieces)
            {
                if (piece == null)
                    continue;

                piece.SetCanBeginDrag(CanBeginPieceDrag);
                piece.Released += HandlePieceReleased;
            }

            _isSubscribed = true;
        }

        public void ProcessInput()
        {
            if (gameSessionController == null || !gameSessionController.IsGameplayActive)
                return;

            if (tutorialController != null && tutorialController.IsGameplayInputLocked)
            {
                boardShiftController?.CancelInput();
                placementPreview?.Hide();
                return;
            }

            if (boardShiftController == null ||
                !boardShiftController.TryReadDirection(BlockPiece.IsAnyDragging, out Vector2Int direction))
            {
                return;
            }

            if (tutorialController != null && !tutorialController.CanShiftBoard(direction))
            {
                tutorialController.NotifyBoardShiftBlocked(direction);
                return;
            }

            ShiftBoard(direction);
        }

        public void UpdatePlacementPreview()
        {
            if (placementPreview == null)
                return;

            BlockPiece activePiece = BlockPiece.ActivePiece;
            if (gameSessionController == null || !gameSessionController.IsGameplayActive ||
                (tutorialController != null && tutorialController.IsGameplayInputLocked) ||
                activePiece == null)
            {
                placementPreview.Hide();
                return;
            }

            placementPreview.Show(blockField, activePiece);
        }

        public void UnsubscribePieces()
        {
            if (!_isSubscribed || pieces == null)
                return;

            foreach (BlockPiece piece in pieces)
            {
                if (piece == null)
                    continue;

                piece.Released -= HandlePieceReleased;
                piece.SetCanBeginDrag(null);
            }

            _isSubscribed = false;
        }

        private void OnDestroy()
        {
            UnsubscribePieces();
        }

        private void HandlePieceReleased(BlockPiece piece)
        {
            placementPreview?.Hide();

            if (piece == null)
                return;

            if (gameSessionController == null || !gameSessionController.IsGameplayActive)
            {
                piece.ReturnToSlot();
                return;
            }

            if (tutorialController != null && !tutorialController.CanInteractWithPiece(piece))
            {
                tutorialController.NotifyPieceInteractionBlocked();
                piece.ReturnToSlot();
                return;
            }

            if (blockField == null || !blockField.TryGetAnchorFor(piece, out Vector2Int anchor) ||
                !blockField.CanInstall(piece, anchor))
            {
                piece.ReturnToSlot();
                return;
            }

            PlacementScoreGuard.PlacementScoreSnapshot snapshot = default;
            bool hasValidationSnapshot = placementScoreGuard != null && playerProgressController != null;
            if (hasValidationSnapshot)
            {
                snapshot = placementScoreGuard.Capture(
                    blockField,
                    piece,
                    playerProgressController.Score,
                    playerProgressController.Gold);
            }

            piece.SnapTo(blockField.GetWorldPosition(anchor));
            blockField.Install(piece, anchor);
            AwardScore(piece.CellCount * 10, false);

            int clearedLines = blockField.ClearCompletedLines(
                _clearedBlockPositions,
                _clearedBlockPoints,
                _clearedBlockSprites);
            NotifyClearedContent();
            if (clearedLines > 0)
            {
                AwardScore(clearedLines * 100 + clearedLines * clearedLines * 50, false);
                lineClearFeedbackController?.PlayPlacementClear(piece, clearedLines, _clearedBlockPositions);
            }

            if (hasValidationSnapshot)
            {
                placementScoreGuard.Validate(
                    snapshot,
                    playerProgressController.Score,
                    playerProgressController.MaxScore,
                    playerProgressController.Gold,
                    playerProgressController.ApplyServerData);
            }

            piece.MarkPlaced();
            stageModeController?.NotifyPiecePlaced();
            tutorialController?.NotifyPiecePlaced();

            // A skill turn advances only after every piece in the current set has been used.
            if (randomBlockManager != null && randomBlockManager.AreAllPiecesPlaced())
                catHomeGrowthController?.CompleteTurn();

            if (gameSessionController.TryCompleteStage() || gameSessionController.TryFailStage())
                return;

            if (randomBlockManager != null && randomBlockManager.AreAllPiecesPlaced() &&
                !randomBlockManager.GiveNewSet(blockField))
            {
                gameSessionController.EndGame(GameOverReason.NoAvailablePlacement);
                return;
            }

            if (randomBlockManager == null || !randomBlockManager.HasAnyAvailablePlacement(blockField))
            {
                gameSessionController.EndGame(GameOverReason.NoAvailablePlacement);
                return;
            }

            RefreshStageMessage();
        }

        private void ShiftBoard(Vector2Int direction)
        {
            bool useSphynxObjectSwipe = catHomeGrowthController != null && catHomeGrowthController.HasSphynxObjectSwipe;
            if (boardShiftController == null ||
                !boardShiftController.TryShift(
                    direction,
                    _clearedBlockPositions,
                    _clearedBlockPoints,
                    _clearedBlockSprites,
                    useSphynxObjectSwipe,
                    out BoardShiftController.BoardShiftResult result))
            {
                tutorialController?.NotifyBoardShiftFailed(direction);
                return;
            }

            tutorialController?.NotifyBoardShiftSucceeded(direction);
            if (useSphynxObjectSwipe)
                catHomeGrowthController.ConsumeSphynxObjectSwipe();

            NotifyClearedContent();
            if (result.ClearedLines > 0)
            {
                int clearScore = result.ClearedLines * 90 + result.ClearedLines * result.ClearedLines * 40;
                AwardScore(clearScore * SwipeClearScoreMultiplier, true);
                lineClearFeedbackController?.PlaySwipeClear(result.ClearedLines, _clearedBlockPositions);
            }

            if (gameSessionController.TryCompleteStage())
                return;

            if (!result.HasAnyRemainingPlacement)
            {
                gameSessionController.EndGame(GameOverReason.NoAvailablePlacement);
                return;
            }

            if (tutorialController == null || !tutorialController.HasPriorityMessage)
            {
                gameSessionController.SetMessage(stageModeController != null && stageModeController.IsStageMode
                    ? stageModeController.GetStartMessage()
                    : result.HasVisibleChange ? "Shift" : string.Empty);
            }
        }

        private bool CanBeginPieceDrag(BlockPiece piece)
        {
            if (gameSessionController == null || !gameSessionController.IsGameplayActive)
                return false;

            if (tutorialController == null || tutorialController.CanInteractWithPiece(piece))
                return true;

            tutorialController.NotifyPieceInteractionBlocked();
            return false;
        }

        private void NotifyClearedContent()
        {
            int cheeseMultiplier = catHomeGrowthController != null ? catHomeGrowthController.CheeseMultiplier : 1;
            stageModeController?.NotifyClearedPoints(_clearedBlockPoints, cheeseMultiplier);
            stageModeController?.NotifyClearedCatSprites(_clearedBlockSprites);
        }

        private void AwardScore(int score, bool syncToServer)
        {
            if (playerProgressController == null || score <= 0)
                return;

            int multiplier = catHomeGrowthController != null ? catHomeGrowthController.ScoreMultiplier : 1;
            playerProgressController.AddScore(score * multiplier, syncToServer);
        }

        private void RefreshStageMessage()
        {
            gameSessionController?.SetMessage(stageModeController != null && stageModeController.IsStageMode
                ? stageModeController.GetStartMessage()
                : string.Empty);
        }
    }
}
