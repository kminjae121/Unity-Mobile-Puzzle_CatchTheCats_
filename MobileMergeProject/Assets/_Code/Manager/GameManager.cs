using System.Collections.Generic;
using _Code.Block;
using _Code.CatHome;
using _Code.Effects;
using _Code.Field;
using _Code.Server;
using _Code.SO;
using _Code.Stage;
using Code.Core;
using Code.Core.Events.Bus;
using Code.Core.Events.Bus.TextEvent;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using MouseView = _Code.Mouse.Mouse;
using TutorialController = _Code.Manager.TutorialController;

namespace _Code.Manager
{
    public class GameManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private BlockField blockField;
        [SerializeField] private RandomBlockManager randomBlockManager;
        [SerializeField] private BlockPiece[] pieces;
        [SerializeField] private MouseView mouse;
        [SerializeField] private JsonManager jsonManager;
        [SerializeField] private ServerScoreClient serverScoreClient;
        [SerializeField] private HapticFeedback hapticFeedback;
        [SerializeField] private BlockPlacementPreview placementPreview;
        [SerializeField] private GameOverView gameOverView;
        [SerializeField] private LineClearPawParticleEffect lineClearParticleEffect;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI bestScoreText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private string resetSceneName = "MainScene";
        [SerializeField, Min(20f)] private float swipeMinDistance = 75f;
        [SerializeField] private bool enableKeyboardInput = true;

        [Header("Responsibility Controllers")]
        [SerializeField] private PlayerProgressController playerProgressController;
        [SerializeField] private StageModeController stageModeController;
        [SerializeField] private BoardShiftController boardShiftController;
        [SerializeField] private LineClearEffectPlayer lineClearEffectPlayer;
        [SerializeField] private PlacementScoreGuard placementScoreGuard;
        [SerializeField] private TutorialController tutorialController;
        [SerializeField] private CatHomeGrowthController catHomeGrowthController;

        private readonly List<Vector3> _clearedBlockPositions = new List<Vector3>(36);
        private readonly List<Vector2Int> _clearedBlockPoints = new List<Vector2Int>(36);
        private readonly List<Sprite> _clearedBlockSprites = new List<Sprite>(36);
        private bool _isGameOver;

        private const int SwipeClearScoreMultiplier = 2;
        private const float SwipeClearEffectSizeMultiplier = 1.4f;

        private static readonly Color BlackCatEffectColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color WhiteCatEffectColor = Color.white;
        private static readonly Color YellowCatEffectColor = new Color(1f, 0.72f, 0.62f, 1f);

        private void Awake()
        {
            if (blockField == null)
            {
                enabled = false;
                return;
            }

            ResolveSceneReferences();
            ResolveControllers();
            ConfigureControllers();
        }

        private void Start()
        {
            if (randomBlockManager == null || playerProgressController == null || stageModeController == null)
                return;

            blockField.Rebuild();
            blockField.ClearAll();
            jsonManager?.ResetCatGrowthLines();
            catHomeGrowthController.Initialize(blockField, jsonManager);
            stageModeController.Initialize(blockField, gameObject);

            randomBlockManager.Initialize(pieces);
            if (mouse != null)
                mouse.Initialize(blockField);

            SubscribePieces();
            playerProgressController.Initialize(stageModeController.HasScoreGoal, stageModeController.TargetScore);
            gameOverView?.Hide();

            if (!randomBlockManager.GiveNewSet(blockField))
            {
                EndGame();
                return;
            }

            bool tutorialStarted = tutorialController != null && tutorialController.TryBegin(stageModeController.IsStageMode);
            if (!tutorialStarted)
                SetMessage(stageModeController.GetStartMessage());
        }

        private void Update()
        {
            if (_isGameOver)
                return;

            if (tutorialController != null && tutorialController.IsGameplayInputLocked)
            {
                boardShiftController?.CancelInput();
                placementPreview?.Hide();
                return;
            }

            if (boardShiftController != null &&
                boardShiftController.TryReadDirection(BlockPiece.IsAnyDragging, out Vector2Int direction))
            {
                if (tutorialController != null && !tutorialController.CanShiftBoard(direction))
                {
                    tutorialController.NotifyBoardShiftBlocked(direction);
                    return;
                }

                ShiftBoard(direction);
            }
        }

        private void LateUpdate()
        {
            UpdatePlacementPreview();
        }

        private void OnDestroy()
        {
            UnsubscribePieces();
        }

        public void StartTutorial()
        {
            if (_isGameOver || tutorialController == null)
                return;

            tutorialController.BeginManually();
        }

        public void ReturnToMainScene()
        {
            _isGameOver = true;
            placementPreview?.Hide();
            playerProgressController?.TryUpdateMaxScore(true);
            StageRunContext.SelectInfiniteMode();

            if (!string.IsNullOrEmpty(resetSceneName))
                SceneManager.LoadScene(resetSceneName);
        }

        public void ReturnToStageSelect()
        {
            _isGameOver = true;
            placementPreview?.Hide();
            playerProgressController?.TryUpdateMaxScore(true);
            StageRunContext.SelectInfiniteMode();

            string stageSelectSceneName = stageModeController != null
                ? stageModeController.StageSelectSceneName
                : "StageScene";
            SceneManager.LoadScene(stageSelectSceneName);
        }

        private void ResolveSceneReferences()
        {
            if (randomBlockManager == null)
                randomBlockManager = GetComponent<RandomBlockManager>();

            if (jsonManager == null)
                jsonManager = GetComponent<JsonManager>();

            if (serverScoreClient == null)
                serverScoreClient = GetComponent<ServerScoreClient>();

            if (hapticFeedback == null)
                hapticFeedback = GetComponent<HapticFeedback>();

            if (gameOverView == null)
                gameOverView = GetComponentInChildren<GameOverView>(true);

            if (placementPreview == null)
                placementPreview = GetComponent<BlockPlacementPreview>();

            if (placementPreview == null)
                placementPreview = gameObject.AddComponent<BlockPlacementPreview>();

            // This reference should be a prefab asset. LineClearEffectPlayer pools prefab instances at runtime.
        }

        private void ResolveControllers()
        {
            if (playerProgressController == null)
                playerProgressController = GetOrAdd<PlayerProgressController>();

            if (stageModeController == null)
                stageModeController = GetOrAdd<StageModeController>();

            if (boardShiftController == null)
                boardShiftController = GetOrAdd<BoardShiftController>();

            if (lineClearEffectPlayer == null)
                lineClearEffectPlayer = GetOrAdd<LineClearEffectPlayer>();

            if (placementScoreGuard == null)
                placementScoreGuard = GetOrAdd<PlacementScoreGuard>();

            if (tutorialController == null)
                tutorialController = GetOrAdd<TutorialController>();

            if (catHomeGrowthController == null)
                catHomeGrowthController = GetOrAdd<CatHomeGrowthController>();
        }

        private void ConfigureControllers()
        {
            playerProgressController.Configure(jsonManager, serverScoreClient, scoreText, bestScoreText);
            boardShiftController.Configure(blockField, randomBlockManager, mouse, swipeMinDistance, enableKeyboardInput);
            lineClearEffectPlayer.Configure(hapticFeedback, lineClearParticleEffect);
            placementScoreGuard.Configure(jsonManager, serverScoreClient, resetSceneName);
            tutorialController.Configure(messageText, blockField, mouse, pieces);
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private void HandlePieceReleased(BlockPiece piece)
        {
            placementPreview?.Hide();

            if (_isGameOver)
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
            
            if (!blockField.TryGetAnchorFor(piece, out Vector2Int anchor) || !blockField.CanInstall(piece, anchor))
            {
                piece.ReturnToSlot();
                return;
            }

            PlacementScoreGuard.PlacementScoreSnapshot snapshot = default;
            bool hasValidationSnapshot = placementScoreGuard != null;

            if (hasValidationSnapshot)
                snapshot = placementScoreGuard.Capture(blockField, piece, playerProgressController.Score, playerProgressController.Gold);

            piece.SnapTo(blockField.GetWorldPosition(anchor));
            blockField.Install(piece, anchor);
            
            playerProgressController.AddScore(piece.CellCount * 10, false);

            int clearedLines = blockField.ClearCompletedLines(_clearedBlockPositions, _clearedBlockPoints, _clearedBlockSprites);
            stageModeController.NotifyClearedPoints(_clearedBlockPoints);
            stageModeController.NotifyClearedCatSprites(_clearedBlockSprites);
            if (clearedLines > 0)
            {
                switch (clearedLines)
                {
                    case 1:
                        Bus<CamShakeEvent>.Raise(new CamShakeEvent(0.1f));
                        Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.Clear));
                        SoundManager.Instance.PlayClip("ClearSound");
                        break;
                    case 2:
                        Bus<CamShakeEvent>.Raise(new CamShakeEvent(0.2f));
                        Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.Double));
                        SoundManager.Instance.PlayClip("ClearSound");
                        
                        break;
                    case 3:
                        Bus<CamShakeEvent>.Raise(new CamShakeEvent(0.3f));
                        Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.Tripple));
                        SoundManager.Instance.PlayClip("ClearSound");

                        break;
                    case 4:
                        Bus<CamShakeEvent>.Raise(new CamShakeEvent(0.4f));
                        Bus<EventTxtEvent>.Raise(new EventTxtEvent(TextType.Quadra));
                        SoundManager.Instance.PlayClip("ClearSound");
                        
                        break;
                }
                playerProgressController.AddScore(clearedLines * 100 + clearedLines * clearedLines * 50, false);
                lineClearEffectPlayer.Play(clearedLines, _clearedBlockPositions, GetLineClearEffectColor(piece));
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

            if (TryCompleteStage())
                return;

            if (TryFailStage())
                return;

            if (randomBlockManager.AreAllPiecesPlaced() && !randomBlockManager.GiveNewSet(blockField))
            {
                EndGame();
                return;
            }

            if (!randomBlockManager.HasAnyAvailablePlacement(blockField))
            {
                EndGame();
                return;
            }

            SetMessage(stageModeController != null && stageModeController.IsStageMode
                ? stageModeController.GetStartMessage()
                : string.Empty);
        }

        private bool ShiftBoard(Vector2Int direction)
        {
            if (boardShiftController == null ||
                !boardShiftController.TryShift(direction, _clearedBlockPositions, _clearedBlockPoints, _clearedBlockSprites, out BoardShiftController.BoardShiftResult result))
            {
                tutorialController?.NotifyBoardShiftFailed(direction);
                return false;
            }

            tutorialController?.NotifyBoardShiftSucceeded(direction);
            stageModeController.NotifyClearedPoints(_clearedBlockPoints);
            stageModeController.NotifyClearedCatSprites(_clearedBlockSprites);

            if (result.ClearedLines > 0)
            {
                int clearScore = result.ClearedLines * 90 + result.ClearedLines * result.ClearedLines * 40;
                playerProgressController.AddScore(clearScore * SwipeClearScoreMultiplier, true);
                lineClearEffectPlayer.Play(
                    result.ClearedLines,
                    _clearedBlockPositions,
                    Color.white,
                    SwipeClearEffectSizeMultiplier);
            }

            if (TryCompleteStage())
                return true;

            if (!result.HasAnyRemainingPlacement)
            {
                EndGame();
                return true;
            }

            if (tutorialController == null || !tutorialController.HasPriorityMessage)
                SetMessage(stageModeController != null && stageModeController.IsStageMode
                    ? stageModeController.GetStartMessage()
                    : result.HasVisibleChange ? "Shift" : string.Empty);

            return true;
        }

        private void SubscribePieces()
        {
            if (pieces == null)
                return;

            foreach (BlockPiece piece in pieces)
            {
                if (piece != null)
                {
                    piece.SetCanBeginDrag(CanBeginPieceDrag);
                    piece.Released += HandlePieceReleased;
                }
            }
        }

        private void UnsubscribePieces()
        {
            if (pieces == null)
                return;

            foreach (BlockPiece piece in pieces)
            {
                if (piece != null)
                {
                    piece.Released -= HandlePieceReleased;
                    piece.SetCanBeginDrag(null);
                }
            }
        }

        private bool CanBeginPieceDrag(BlockPiece piece)
        {
            if (_isGameOver)
                return false;

            if (tutorialController == null || tutorialController.CanInteractWithPiece(piece))
                return true;

            tutorialController.NotifyPieceInteractionBlocked();
            return false;
        }

        private bool TryCompleteStage()
        {
            if (_isGameOver || stageModeController == null || !stageModeController.IsComplete(playerProgressController.Score))
                return false;

            CompleteStage();
            return true;
        }

        private bool TryFailStage()
        {
            if (_isGameOver || stageModeController == null || !stageModeController.IsFailedByPlacementLimit)
                return false;

            EndGame();
            return true;
        }

        private static Color GetLineClearEffectColor(BlockPiece piece)
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

            return GetLineClearEffectColorByName(blockSprite.name);
        }

        private static Color GetLineClearEffectColorByName(string spriteName)
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

        private void CompleteStage()
        {
            _isGameOver = true;
            placementPreview?.Hide();
            playerProgressController.TryUpdateMaxScore(true);
            stageModeController?.MarkCleared();
            SetMessage(stageModeController.GetClearMessage());

            if (gameOverView != null)
                gameOverView.ShowStageClearPrompt(
                    playerProgressController.Score,
                    playerProgressController.MaxScore,
                    stageModeController.GetNextSceneNameAfterClear());
            else
                SceneManager.LoadScene(stageModeController.StageSelectSceneName);
        }

        private void EndGame()
        {
            _isGameOver = true;
            placementPreview?.Hide();
            playerProgressController.TryUpdateMaxScore(true);
            SetMessage(string.Empty);

            if (gameOverView != null && stageModeController != null && stageModeController.IsStageMode)
                gameOverView.ShowStageFailed(
                    playerProgressController.Score,
                    playerProgressController.MaxScore,
                    stageModeController.StageSelectSceneName);
            else if (gameOverView != null)
                gameOverView.Show(playerProgressController.Score, playerProgressController.MaxScore);
            else
                SetMessage("Game Over");
        }

        private void UpdatePlacementPreview()
        {
            if (placementPreview == null)
                return;

            BlockPiece activePiece = BlockPiece.ActivePiece;
            if (_isGameOver ||
                (tutorialController != null && tutorialController.IsGameplayInputLocked) ||
                activePiece == null)
            {
                placementPreview.Hide();
                return;
            }

            placementPreview.Show(blockField, activePiece);
        }

        private void SetMessage(string message)
        {
            if (messageText != null)
                messageText.text = message;
        }
    }
}
