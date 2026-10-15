using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Code.Manager
{
    public sealed class GameOverView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI scoreLabelText;
        [SerializeField] private TextMeshProUGUI scoreValueText;
        [SerializeField] private TextMeshProUGUI bestScoreLabelText;
        [SerializeField] private TextMeshProUGUI bestScoreValueText;
        [SerializeField] private TextMeshProUGUI reasonText;
        [SerializeField] private Button restartButton;

        private const string GameOverTitle = "GAME OVER";
        private const string StageClearTitle = "\uD074\uB9AC\uC5B4 \uD558\uC168\uC2B5\uB2C8\uB2E4";
        private const string StageFailedTitle = "\uAC8C\uC784\uC624\uBC84 \uD558\uC168\uC2B5\uB2C8\uB2E4";
        private const string ScoreLabel = "\uC810\uC218";
        private const string BestScoreLabel = "\uCD5C\uACE0\uC810\uC218";
        private const string RetryButtonLabel = "";
        private const string NextButtonLabel = "\uB2E4\uC74C";
        private const string StageExitButtonLabel = "\uB098\uAC00\uAE30";

        private TextMeshProUGUI _primaryButtonLabelText;
        private PrimaryButtonAction _primaryButtonAction = PrimaryButtonAction.RestartCurrentScene;
        private string _targetSceneName;
        private Coroutine _reasonAnimation;

        private enum PrimaryButtonAction
        {
            RestartCurrentScene,
            LoadScene
        }

        private void Awake()
        {
            ResolveOptionalReferences();

            if (restartButton != null)
                restartButton.onClick.AddListener(HandlePrimaryButtonClicked);
        }

        private void OnDestroy()
        {
            if (restartButton != null)
                restartButton.onClick.RemoveListener(HandlePrimaryButtonClicked);
        }

        public void Show(int score, int bestScore)
        {
            Show(score, bestScore, GameOverTitle, GameOverReason.NoAvailablePlacement);
        }

        public void Show(int score, int bestScore, GameOverReason reason)
        {
            Show(score, bestScore, GameOverTitle, reason);
        }

        public void ShowStageClear(int score, int bestScore)
        {
            Show(score, bestScore, StageClearTitle, null);
        }

        public void ShowStageClearPrompt(int score, int bestScore, string nextSceneName)
        {
            ShowResult(
                StageClearTitle,
                ScoreLabel,
                score.ToString(),
                "\uB2E4\uC74C\uC73C\uB85C \uB118\uC5B4\uAC00\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?",
                bestScore.ToString(),
                NextButtonLabel,
                nextSceneName,
                string.Empty);
        }

        public void ShowStageFailed(int score, int bestScore, string stageSelectSceneName)
        {
            ShowStageFailed(score, bestScore, stageSelectSceneName, GameOverReason.PlacementLimit, 0, 0, 0);
        }

        public void ShowStageFailed(
            int score,
            int bestScore,
            string stageSelectSceneName,
            GameOverReason reason,
            int remainingCheeseCount,
            int usedPlacementCount,
            int maxPlacementCount)
        {
            ShowResult(
                StageFailedTitle,
                ScoreLabel,
                score.ToString(),
                BestScoreLabel,
                bestScore.ToString(),
                StageExitButtonLabel,
                stageSelectSceneName,
                BuildReasonMessage(reason, remainingCheeseCount, usedPlacementCount, maxPlacementCount));
        }

        private void Show(int score, int bestScore, string title, GameOverReason? reason)
        {
            ShowResult(
                title,
                ScoreLabel,
                score.ToString(),
                BestScoreLabel,
                bestScore.ToString(),
                RetryButtonLabel,
                null,
                reason.HasValue ? BuildReasonMessage(reason.Value, 0, 0, 0) : string.Empty);
        }

        private void ShowResult(
            string title,
            string scoreLabel,
            string scoreValue,
            string bestScoreLabel,
            string bestScoreValue,
            string buttonLabel,
            string targetSceneName,
            string reasonMessage)
        {
            if (scoreValueText != null)
                scoreValueText.text = scoreValue;

            if (bestScoreValueText != null)
                bestScoreValueText.text = bestScoreValue;

            if (scoreLabelText != null)
                scoreLabelText.text = scoreLabel;

            if (bestScoreLabelText != null)
                bestScoreLabelText.text = bestScoreLabel;

            if (titleText != null)
                titleText.text = title;

            if (_primaryButtonLabelText != null)
                _primaryButtonLabelText.text = buttonLabel;

            _targetSceneName = targetSceneName;
            _primaryButtonAction = string.IsNullOrEmpty(targetSceneName)
                ? PrimaryButtonAction.RestartCurrentScene
                : PrimaryButtonAction.LoadScene;

            gameObject.SetActive(true);
            SetReasonText(reasonMessage);
        }

        public void Hide()
        {
            if (_reasonAnimation != null)
            {
                StopCoroutine(_reasonAnimation);
                _reasonAnimation = null;
            }

            gameObject.SetActive(false);
        }

        private void HandlePrimaryButtonClicked()
        {
            if (_primaryButtonAction == PrimaryButtonAction.LoadScene && !string.IsNullOrEmpty(_targetSceneName))
            {
                SceneManager.LoadScene(_targetSceneName);
                return;
            }

            RestartCurrentScene();
        }

        private static void RestartCurrentScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();

            if (currentScene.buildIndex >= 0)
                SceneManager.LoadScene(currentScene.buildIndex);
            else
                SceneManager.LoadScene(currentScene.name);
        }

        private void SetReasonText(string message)
        {
            if (reasonText == null)
                return;

            if (_reasonAnimation != null)
            {
                StopCoroutine(_reasonAnimation);
                _reasonAnimation = null;
            }

            reasonText.text = message;
            reasonText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            reasonText.rectTransform.localScale = Vector3.one;

            if (!string.IsNullOrEmpty(message))
                _reasonAnimation = StartCoroutine(AnimateReasonText());
        }

        private IEnumerator AnimateReasonText()
        {
            const float duration = 0.24f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(1.12f, 1f, Mathf.SmoothStep(0f, 1f, t));
                reasonText.rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }

            reasonText.rectTransform.localScale = Vector3.one;
            _reasonAnimation = null;
        }

        private void ResolveOptionalReferences()
        {
            if (scoreLabelText == null)
                scoreLabelText = FindText("GameOverScoreLabelTxt");

            if (bestScoreLabelText == null)
                bestScoreLabelText = FindText("GameOverBestScoreLabelTxt");

            if (reasonText == null)
                reasonText = FindText("GameOverReasonTxt");

            if (reasonText == null)
                reasonText = CreateReasonText(transform);

            if (_primaryButtonLabelText == null && restartButton != null)
                _primaryButtonLabelText = restartButton.GetComponentInChildren<TextMeshProUGUI>(true);

            if (_primaryButtonLabelText == null && restartButton != null)
                _primaryButtonLabelText = CreateButtonLabel(restartButton.transform);
        }

        private TextMeshProUGUI FindText(string objectName)
        {
            Transform child = transform.Find(objectName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private TextMeshProUGUI CreateReasonText(Transform parent)
        {
            GameObject labelObject = new GameObject("GameOverReasonTxt", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 205f);
            rectTransform.sizeDelta = new Vector2(820f, 160f);

            TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
            if (titleText != null && titleText.font != null)
                text.font = titleText.font;

            if (titleText != null && titleText.fontSharedMaterial != null)
                text.fontSharedMaterial = titleText.fontSharedMaterial;

            text.fontSize = 52f;
            text.fontWeight = FontWeight.Bold;
            text.color = new Color(1f, 0.87f, 0.42f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = 4f;
            text.margin = new Vector4(24f, 10f, 24f, 10f);
            text.raycastTarget = false;
            return text;
        }

        private TextMeshProUGUI CreateButtonLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("GameOverButtonLabelTxt", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
            if (titleText != null && titleText.font != null)
                text.font = titleText.font;

            if (titleText != null && titleText.fontSharedMaterial != null)
                text.fontSharedMaterial = titleText.fontSharedMaterial;

            text.text = RetryButtonLabel;
            text.fontSize = 42f;
            text.color = new Color(0.31f, 0.16f, 0.08f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static string BuildReasonMessage(
            GameOverReason reason,
            int remainingCheeseCount,
            int usedPlacementCount,
            int maxPlacementCount)
        {
            if (reason == GameOverReason.PlacementLimit)
            {
                return $"\uBC30\uCE58 \uC81C\uD55C\uC744 \uBAA8\uB450 \uC0AC\uC6A9\uD588\uC9C0\uB9CC \uCE58\uC988\uAC00 \uB0A8\uC544 \uC788\uC5B4\uC694.\n\uB0A8\uC740 \uCE58\uC988 {remainingCheeseCount}\uAC1C  \u00B7  \uBC30\uCE58 {usedPlacementCount}/{maxPlacementCount}";
            }

            return "\uB354 \uC774\uC0C1 \uBE14\uB7ED\uC744 \uB193\uC744 \uC218 \uC788\uB294 \uACF5\uAC04\uC774 \uC5C6\uC5B4\uC694.";
        }
    }
}
