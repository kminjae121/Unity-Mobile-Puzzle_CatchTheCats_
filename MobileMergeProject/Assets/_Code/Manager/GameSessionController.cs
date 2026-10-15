using System.Collections;
using _Code.CatHome;
using _Code.Field;
using _Code.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Code.Manager
{
    public enum GameOverReason
    {
        NoAvailablePlacement,
        PlacementLimit
    }

    public sealed class GameSessionController : MonoBehaviour
    {
        [SerializeField] private BlockField blockField;
        [SerializeField] private RandomBlockManager randomBlockManager;
        [SerializeField] private PlayerProgressController playerProgressController;
        [SerializeField] private StageModeController stageModeController;
        [SerializeField] private CatHomeGrowthController catHomeGrowthController;
        [SerializeField] private BlockPlacementPreview placementPreview;
        [SerializeField] private GameOverView gameOverView;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private string resetSceneName = "MainScene";

        private const int PlacementFailureBackdropSortingOrder = 900;
        private static readonly Color PlacementFailureBackdropColor = new Color(0.04f, 0.05f, 0.09f, 0.62f);
        private static Sprite _placementFailureBackdropSprite;

        private bool _isGameOver;
        private bool _isRunning;
        private GameObject _placementFailureBackdrop;
        private SpriteRenderer _placementFailureBackdropRenderer;

        public bool IsGameOver => _isGameOver;
        public bool IsGameplayActive => _isRunning && !_isGameOver;

        public void Configure(
            BlockField blockField,
            RandomBlockManager randomBlockManager,
            PlayerProgressController playerProgressController,
            StageModeController stageModeController,
            CatHomeGrowthController catHomeGrowthController,
            BlockPlacementPreview placementPreview,
            GameOverView gameOverView,
            TextMeshProUGUI messageText,
            string resetSceneName)
        {
            if (this.blockField == null)
                this.blockField = blockField;

            if (this.randomBlockManager == null)
                this.randomBlockManager = randomBlockManager;

            if (this.playerProgressController == null)
                this.playerProgressController = playerProgressController;

            if (this.stageModeController == null)
                this.stageModeController = stageModeController;

            if (this.catHomeGrowthController == null)
                this.catHomeGrowthController = catHomeGrowthController;

            if (this.placementPreview == null)
                this.placementPreview = placementPreview;

            if (this.gameOverView == null)
                this.gameOverView = gameOverView;

            if (this.messageText == null)
                this.messageText = messageText;

            if (!string.IsNullOrWhiteSpace(resetSceneName))
                this.resetSceneName = resetSceneName;
        }

        public void BeginRun()
        {
            _isGameOver = false;
            _isRunning = true;
            randomBlockManager?.ClearPlacementHighlights();
            HidePlacementFailureBackdrop();
            placementPreview?.Hide();
            gameOverView?.Hide();
        }

        public bool TryCompleteStage()
        {
            if (_isGameOver || stageModeController == null || playerProgressController == null ||
                !stageModeController.IsComplete(playerProgressController.Score))
            {
                return false;
            }

            CompleteStage();
            return true;
        }

        public bool TryFailStage()
        {
            if (_isGameOver || stageModeController == null || !stageModeController.IsFailedByPlacementLimit)
                return false;

            EndGame(GameOverReason.PlacementLimit);
            return true;
        }

        public void EndGame(GameOverReason reason = GameOverReason.NoAvailablePlacement)
        {
            if (TryUseWhiteCatLife())
                return;

            _isGameOver = true;
            _isRunning = false;
            placementPreview?.Hide();
            playerProgressController?.TryUpdateMaxScore(true);
            SetMessage(string.Empty);
            gameOverView?.Hide();

            if (reason == GameOverReason.NoAvailablePlacement)
            {
                randomBlockManager?.HighlightUnplaceablePieces(blockField);
                ShowPlacementFailureBackdrop();
                StartCoroutine(ShowGameOverAfterPlacementAnimation(reason));
                return;
            }

            ShowGameOver(reason);
        }

        private IEnumerator ShowGameOverAfterPlacementAnimation(GameOverReason reason)
        {
            if (randomBlockManager != null)
                yield return randomBlockManager.PlayUnplaceablePiecesAnimation(blockField);

            HidePlacementFailureBackdrop();
            ShowGameOver(reason);
        }

        private void ShowGameOver(GameOverReason reason)
        {

            if (gameOverView != null && stageModeController != null && stageModeController.IsStageMode && playerProgressController != null)
            {
                gameOverView.ShowStageFailed(
                    playerProgressController.Score,
                    playerProgressController.MaxScore,
                    stageModeController.StageSelectSceneName,
                    reason,
                    stageModeController.RemainingCheeseCount,
                    stageModeController.UsedPlacementCount,
                    stageModeController.MaxPlacementCount);
            }
            else if (gameOverView != null && playerProgressController != null)
            {
                gameOverView.Show(playerProgressController.Score, playerProgressController.MaxScore, reason);
            }
            else
            {
                SetMessage("Game Over");
            }
        }

        public void ReturnToMainScene()
        {
            _isGameOver = true;
            _isRunning = false;
            HidePlacementFailureBackdrop();
            placementPreview?.Hide();
            playerProgressController?.TryUpdateMaxScore(true);
            StageRunContext.SelectInfiniteMode();

            if (!string.IsNullOrEmpty(resetSceneName))
                SceneManager.LoadScene(resetSceneName);
        }

        public void ReturnToStageSelect()
        {
            _isGameOver = true;
            _isRunning = false;
            HidePlacementFailureBackdrop();
            placementPreview?.Hide();
            playerProgressController?.TryUpdateMaxScore(true);
            StageRunContext.SelectInfiniteMode();

            string stageSelectSceneName = stageModeController != null
                ? stageModeController.StageSelectSceneName
                : "StageScene";
            SceneManager.LoadScene(stageSelectSceneName);
        }

        public void SetMessage(string message)
        {
            if (messageText != null)
                messageText.text = message;
        }

        private void ShowPlacementFailureBackdrop()
        {
            Camera targetCamera = Camera.main;
            if (targetCamera == null)
                return;

            if (_placementFailureBackdrop == null)
            {
                _placementFailureBackdrop = new GameObject("PlacementFailureBackdrop");
                _placementFailureBackdropRenderer = _placementFailureBackdrop.AddComponent<SpriteRenderer>();
                _placementFailureBackdropRenderer.sortingOrder = PlacementFailureBackdropSortingOrder;
            }

            if (_placementFailureBackdropSprite == null)
                _placementFailureBackdropSprite = CreateBackdropSprite();

            float height = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 20f;
            float width = targetCamera.orthographic
                ? height * targetCamera.aspect
                : 20f;

            Vector3 position = targetCamera.transform.position;
            position.z = blockField != null ? blockField.transform.position.z + 0.5f : 0f;
            _placementFailureBackdrop.transform.position = position;
            _placementFailureBackdrop.transform.localScale = new Vector3(width, height, 1f);
            _placementFailureBackdropRenderer.sprite = _placementFailureBackdropSprite;
            _placementFailureBackdropRenderer.color = PlacementFailureBackdropColor;
            _placementFailureBackdropRenderer.gameObject.SetActive(true);
        }

        private void HidePlacementFailureBackdrop()
        {
            if (_placementFailureBackdropRenderer != null)
                _placementFailureBackdropRenderer.gameObject.SetActive(false);
        }

        private static Sprite CreateBackdropSprite()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 1f);
        }

        private void CompleteStage()
        {
            _isGameOver = true;
            _isRunning = false;
            placementPreview?.Hide();
            playerProgressController?.TryUpdateMaxScore(true);
            stageModeController?.MarkCleared();
            SetMessage(stageModeController != null ? stageModeController.GetClearMessage() : string.Empty);

            if (gameOverView != null && playerProgressController != null && stageModeController != null)
            {
                gameOverView.ShowStageClearPrompt(
                    playerProgressController.Score,
                    playerProgressController.MaxScore,
                    stageModeController.GetNextSceneNameAfterClear());
            }
            else if (stageModeController != null)
            {
                SceneManager.LoadScene(stageModeController.StageSelectSceneName);
            }
        }

        private bool TryUseWhiteCatLife()
        {
            if (catHomeGrowthController == null || !catHomeGrowthController.HasWhiteExtraLife ||
                blockField == null || randomBlockManager == null)
            {
                return false;
            }

            blockField.ClearCatBlocks();
            stageModeController?.GrantExtraPlacementAttempt();
            if (!randomBlockManager.GiveNewSet(blockField) || !catHomeGrowthController.TryUseWhiteExtraLife())
                return false;

            _isGameOver = false;
            placementPreview?.Hide();
            gameOverView?.Hide();
            SetMessage("하얀 고양이의 생명으로 한 번 더 도전해요!");
            return true;
        }
    }
}
