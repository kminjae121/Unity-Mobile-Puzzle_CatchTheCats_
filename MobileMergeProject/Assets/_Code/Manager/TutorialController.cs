using System.Collections;
using System.Collections.Generic;
using _Code.Block;
using _Code.CatHome;
using _Code.Field;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MouseView = _Code.Mouse.Mouse;

namespace _Code.Manager
{
    public sealed class TutorialController : MonoBehaviour
    {
        private const string TutorialCompletedKey = "CatchTheCats.MouseMoveTutorial.Completed.v5";

        private enum StepKind
        {
            Message,
            WaitForShift,
            WaitForPiecePlaced,
            WaitForMatchingLineClear,
            WaitForIndividualSplit,
            Finish
        }

        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private bool showFirstRunTutorial = true;
        [SerializeField] private Color panelColor = new Color(1f, 0.93f, 0.78f, 0.96f);
        [SerializeField] private Color panelOutlineColor = new Color(0.58f, 0.28f, 0.16f, 1f);
        [SerializeField] private Color titleColor = new Color(0.31f, 0.16f, 0.08f, 1f);
        [SerializeField] private Color bodyColor = new Color(0.24f, 0.17f, 0.12f, 1f);
        [SerializeField] private Color guideColor = new Color(1f, 0.78f, 0.18f, 1f);
        [SerializeField] private Color guideShadowColor = new Color(0.43f, 0.21f, 0.08f, 0.72f);
        [SerializeField, Min(4f)] private float guideLineWidth = 12f;
        [SerializeField, Min(18f)] private float guidePointerSize = 112f;
        [SerializeField, Min(2f)] private float guideFocusThickness = 6f;
        [SerializeField, Min(0f)] private float guideFocusPadding = 12f;
        [SerializeField, Min(0.1f)] private float guideAnimationSpeed = 0.675f;

        private readonly List<TutorialStep> _steps = new List<TutorialStep>();
        private readonly List<FocusBox> _focusBoxes = new List<FocusBox>();
        private readonly List<Vector3> _placementTargetPositions = new List<Vector3>(9);

        private GameObject _root;
        private GameObject _dialoguePanel;
        private GameObject _mousePortrait;
        private RectTransform _rootRect;
        private RectTransform _guideRootRect;
        private Image _guideShaft;
        private TextMeshProUGUI _guideHeadText;
        private Image _guidePointer;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _progressText;
        private TextMeshProUGUI _tapHintText;
        private Button _tapAdvanceButton;
        private Button _closeButton;
        private int _stepIndex;
        private bool _isRunning;
        private bool _isManualRun;
        private bool _isMissionActive;
        private bool _guideVisible;
        private Vector2 _guideStartLocal;
        private Vector2 _guideEndLocal;
        private BlockField _blockField;
        private MouseView _mouse;
        private BlockPiece[] _pieces;
        private PlayerProgressController _playerProgressController;
        private CatHomeGrowthController _catHomeGrowthController;
        private Coroutine _tutorialResetRoutine;
        private Camera _worldCamera;
        private Sprite _guidePointerSprite;
        private Sprite _speechBubbleSprite;
        private Texture2D _speechBubbleTexture;
        private bool _fieldEventsSubscribed;

        public bool HasPriorityMessage => _isRunning;
        public bool IsGameplayInputLocked => _isRunning && !_isMissionActive;
        public bool ShouldStartAutomatically =>
            showFirstRunTutorial && PlayerPrefs.GetInt(TutorialCompletedKey, 0) != 1;

        private void Awake()
        {
            BuildSteps();
        }

        private void Update()
        {
            if (!_isRunning || _root == null || !_root.activeSelf)
                return;

            RefreshGuide();
            AnimateGuide();
        }

        private void OnDisable()
        {
            HideView();
            ClearMessage();
        }

        private void OnDestroy()
        {
            UnsubscribeFromFieldEvents();
            if (_tutorialResetRoutine != null)
                StopCoroutine(_tutorialResetRoutine);
        }

        public void Configure(TextMeshProUGUI messageText)
        {
            Configure(messageText, _blockField, _mouse, _pieces, _playerProgressController, _catHomeGrowthController);
        }

        public void Configure(TextMeshProUGUI messageText, BlockField blockField, MouseView mouse, BlockPiece[] pieces)
        {
            Configure(messageText, blockField, mouse, pieces, _playerProgressController, _catHomeGrowthController);
        }

        public void Configure(
            TextMeshProUGUI messageText,
            BlockField blockField,
            MouseView mouse,
            BlockPiece[] pieces,
            PlayerProgressController playerProgressController,
            CatHomeGrowthController catHomeGrowthController)
        {
            if (this.messageText == null)
                this.messageText = messageText;

            if (blockField != null && _blockField != blockField)
            {
                UnsubscribeFromFieldEvents();
                _blockField = blockField;
            }

            if (_mouse == null)
                _mouse = mouse;

            if (_pieces == null || _pieces.Length == 0)
                _pieces = pieces;

            if (_playerProgressController == null)
                _playerProgressController = playerProgressController;

            if (_catHomeGrowthController == null)
                _catHomeGrowthController = catHomeGrowthController;

            SubscribeToFieldEvents();

            EnsureView();
            HideView();
        }

        private void SubscribeToFieldEvents()
        {
            if (_fieldEventsSubscribed || _blockField == null)
                return;

            _blockField.MatchingCatLinesCleared += HandleMatchingCatLinesCleared;
            _blockField.SingleCellBlocksCreated += HandleSingleCellBlocksCreated;
            _fieldEventsSubscribed = true;
        }

        private void UnsubscribeFromFieldEvents()
        {
            if (!_fieldEventsSubscribed || _blockField == null)
                return;

            _blockField.MatchingCatLinesCleared -= HandleMatchingCatLinesCleared;
            _blockField.SingleCellBlocksCreated -= HandleSingleCellBlocksCreated;
            _fieldEventsSubscribed = false;
        }

        public bool TryBegin(bool isStageMode)
        {
            if (!showFirstRunTutorial || PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1)
                return false;

            Begin(false);
            return true;
        }

        public void BeginManually()
        {
            Begin(true);
        }

        public void NotifyBoardShiftSucceeded(Vector2Int direction)
        {
            if (!_isRunning || !_isMissionActive || CurrentStep.Kind != StepKind.WaitForShift)
                return;

            direction = NormalizeDirection(direction);

            if (direction == CurrentStep.Direction)
            {
                AdvanceStep();
                return;
            }

            SetTemporaryHint($"{DirectionToKorean(CurrentStep.Direction)} 방향으로 밀어볼게요.");
            RefreshGuide();
        }

        public void NotifyBoardShiftFailed(Vector2Int direction)
        {
            if (!_isRunning || !_isMissionActive || CurrentStep.Kind != StepKind.WaitForShift)
                return;

            SetTemporaryHint("그쪽 쥐구멍으로는 갈 수 없어요. 화살표 방향으로 다시 밀어봐요.");
            RefreshGuide();
        }

        public void NotifyPiecePlaced()
        {
            if (!_isRunning || !_isMissionActive || CurrentStep.Kind != StepKind.WaitForPiecePlaced)
                return;

            AdvanceStep();
        }

        private void HandleMatchingCatLinesCleared(IReadOnlyList<Sprite> matchingSprites)
        {
            if (!_isRunning || !_isMissionActive || CurrentStep.Kind != StepKind.WaitForMatchingLineClear)
                return;

            AdvanceStep();
        }

        private void HandleSingleCellBlocksCreated()
        {
            if (!_isRunning || !_isMissionActive || CurrentStep.Kind != StepKind.WaitForIndividualSplit)
                return;

            Complete();
        }

        public bool CanShiftBoard()
        {
            if (!_isRunning || _steps.Count == 0)
                return true;

            return !IsGameplayInputLocked && CurrentStep.Kind == StepKind.WaitForShift;
        }

        public bool CanShiftBoard(Vector2Int direction)
        {
            if (!_isRunning || _steps.Count == 0)
                return true;

            return !IsGameplayInputLocked &&
                   CurrentStep.Kind == StepKind.WaitForShift &&
                   NormalizeDirection(direction) == CurrentStep.Direction;
        }

        public bool CanInteractWithPiece(BlockPiece piece)
        {
            if (!_isRunning || _steps.Count == 0)
                return true;

            bool acceptsPiecePlacement = CurrentStep.Kind == StepKind.WaitForPiecePlaced ||
                                         CurrentStep.Kind == StepKind.WaitForMatchingLineClear ||
                                         CurrentStep.Kind == StepKind.WaitForIndividualSplit;

            return !IsGameplayInputLocked &&
                   acceptsPiecePlacement &&
                   piece != null &&
                   !piece.IsPlaced;
        }

        public void NotifyBoardShiftBlocked(Vector2Int direction)
        {
            if (!_isRunning || !_isMissionActive || _steps.Count == 0)
                return;

            SetTemporaryHint(GetCurrentActionHint());
            RefreshGuide();
        }

        public void NotifyPieceInteractionBlocked()
        {
            if (!_isRunning || !_isMissionActive || _steps.Count == 0)
                return;

            SetTemporaryHint(GetCurrentActionHint());
            RefreshGuide();
        }

        private TutorialStep CurrentStep => _steps[Mathf.Clamp(_stepIndex, 0, _steps.Count - 1)];

        private void Begin(bool isManualRun)
        {
            BuildSteps();

            if (_steps.Count == 0)
                return;

            _isRunning = true;
            _isManualRun = isManualRun;
            _stepIndex = 0;
            EnsureView();
            ShowCurrentStep();
        }

        private void AdvanceStep()
        {
            if (!_isRunning)
                return;

            _stepIndex++;

            if (_stepIndex >= _steps.Count)
            {
                Complete();
                return;
            }

            ShowCurrentStep();
        }

        private void Complete()
        {
            ScheduleTutorialRunStateReset();
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.Save();

            _isRunning = false;
            HideView();
            ClearMessage();
        }

        private void Close()
        {
            ResetTutorialRunState();
            if (!_isManualRun)
            {
                PlayerPrefs.SetInt(TutorialCompletedKey, 1);
                PlayerPrefs.Save();
            }

            _isRunning = false;
            HideView();
            ClearMessage();
        }

        private void ResetTutorialRunState()
        {
            _playerProgressController?.ResetCurrentScore();
            _catHomeGrowthController?.ResetGrowthForTutorial();
        }

        private void ScheduleTutorialRunStateReset()
        {
            if (_tutorialResetRoutine != null)
                StopCoroutine(_tutorialResetRoutine);

            _tutorialResetRoutine = StartCoroutine(ResetTutorialRunStateAtEndOfFrame());
        }

        private IEnumerator ResetTutorialRunStateAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            ResetTutorialRunState();
            _tutorialResetRoutine = null;
        }

        private void ShowCurrentStep()
        {
            EnsureView();

            if (_root == null)
                return;

            TutorialStep step = CurrentStep;
            _isMissionActive = false;
            _root.SetActive(true);
            SetDialogueVisible(true);

            if (_titleText != null)
                _titleText.text = $"쥐 · {step.Title}";

            if (_bodyText != null)
                _bodyText.text = step.Body;

            if (_progressText != null)
                _progressText.text = $"{_stepIndex + 1}/{_steps.Count}";

            if (_tapAdvanceButton != null)
                _tapAdvanceButton.gameObject.SetActive(true);

            if (_tapHintText != null)
            {
                _tapHintText.gameObject.SetActive(true);
                _tapHintText.text = GetTapHint(step.Kind);
            }

            if (messageText != null)
                messageText.text = step.ShortMessage;

            RefreshGuide();
        }

        private void HandleDialogueTapped()
        {
            if (!_isRunning)
                return;

            StepKind stepKind = CurrentStep.Kind;
            bool isActionStep = stepKind == StepKind.WaitForShift ||
                                stepKind == StepKind.WaitForPiecePlaced ||
                                stepKind == StepKind.WaitForMatchingLineClear ||
                                stepKind == StepKind.WaitForIndividualSplit;

            if (!isActionStep)
            {
                AdvanceStep();
                return;
            }

            _isMissionActive = true;
            SetDialogueVisible(false);
            ClearMessage();

            if (_tapAdvanceButton != null)
                _tapAdvanceButton.gameObject.SetActive(false);

            RefreshGuide();
        }

        private void SetDialogueVisible(bool visible)
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(visible);

            if (_mousePortrait != null)
                _mousePortrait.SetActive(visible);
        }

        private static string GetTapHint(StepKind stepKind)
        {
            if (stepKind == StepKind.Finish)
                return "터치해서 끝내기";

            if (stepKind == StepKind.WaitForShift ||
                stepKind == StepKind.WaitForPiecePlaced ||
                stepKind == StepKind.WaitForMatchingLineClear ||
                stepKind == StepKind.WaitForIndividualSplit)
                return "터치해서 임무 시작";

            return "터치해서 계속";
        }

        private void SetTemporaryHint(string hint)
        {
            if (_bodyText != null)
                _bodyText.text = $"{CurrentStep.Body}\n<size=78%><color=#8A3D25>{hint}</color></size>";

            if (messageText != null)
                messageText.text = hint;
        }

        private string GetCurrentActionHint()
        {
            TutorialStep step = CurrentStep;

            if (step.Kind == StepKind.WaitForPiecePlaced)
                return "먼저 고양이 블럭을 놓아볼게요.";

            if (step.Kind == StepKind.WaitForMatchingLineClear)
                return "같은 색 고양이로 한 줄을 완성해볼게요.";

            if (step.Kind == StepKind.WaitForIndividualSplit)
                return "블럭의 일부만 줄에 걸치도록 놓아볼게요.";

            if (step.Kind == StepKind.WaitForShift)
                return $"{DirectionToKorean(step.Direction)} 방향으로만 밀어볼게요.";

            if (step.Kind == StepKind.Finish)
                return "화면을 터치해 튜토리얼을 마무리해요.";

            return "화면을 터치해서 다음 설명을 볼게요.";
        }

        private void HideView()
        {
            _isMissionActive = false;

            if (_root != null)
                _root.SetActive(false);

            HideGuide();
        }

        private void ClearMessage()
        {
            if (messageText != null)
                messageText.text = string.Empty;
        }

        private void EnsureView()
        {
            if (_root != null)
                return;

            if (targetCanvas == null)
                targetCanvas = FindFirstObjectByType<Canvas>();

            if (targetCanvas == null)
                return;

            _root = new GameObject("TutorialOverlay", typeof(RectTransform));
            _rootRect = _root.GetComponent<RectTransform>();
            _rootRect.SetParent(targetCanvas.transform, false);
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;

            _tapAdvanceButton = CreateTapAdvanceArea(_rootRect);
            _tapAdvanceButton.onClick.AddListener(HandleDialogueTapped);

            CreateGuideView(_rootRect);

            GameObject panelObject = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
            _dialoguePanel = panelObject;
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(_rootRect, false);
            panelRect.anchorMin = new Vector2(0.16f, 0.045f);
            panelRect.anchorMax = new Vector2(0.96f, 0.3f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = panelColor;
            panelImage.raycastTarget = false;
            panelImage.sprite = GetSpeechBubbleSprite();
            panelImage.type = Image.Type.Sliced;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = panelOutlineColor;
            panelOutline.effectDistance = new Vector2(4f, -4f);

            CreateSpeechBubbleTail(panelRect);
            _titleText = CreateText(panelRect, "TutorialTitleTxt", new Vector2(0.08f, 0.7f), new Vector2(0.82f, 0.94f), 60f, titleColor, TextAlignmentOptions.Left);
            _bodyText = CreateText(panelRect, "TutorialBodyTxt", new Vector2(0.08f, 0.24f), new Vector2(0.88f, 0.72f), 55f, bodyColor, TextAlignmentOptions.Left);
            _progressText = CreateText(panelRect, "TutorialProgressTxt", new Vector2(0.08f, 0.05f), new Vector2(0.28f, 0.22f), 28f, bodyColor, TextAlignmentOptions.Left);
            _tapHintText = CreateText(panelRect, "TutorialTapHintTxt", new Vector2(0.52f, 0.05f), new Vector2(0.88f, 0.22f), 28f, titleColor, TextAlignmentOptions.Right);

            _closeButton = CreateButton(panelRect, "TutorialCloseButton", new Vector2(0.89f, 0.73f), new Vector2(0.97f, 0.93f), "X");
            _closeButton.onClick.AddListener(Close);

            CreateMousePortrait(_rootRect);

            HideView();
        }

        private Button CreateTapAdvanceArea(RectTransform parent)
        {
            GameObject tapObject = new GameObject("TutorialTapAdvanceArea", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform tapRect = tapObject.GetComponent<RectTransform>();
            tapRect.SetParent(parent, false);
            tapRect.anchorMin = Vector2.zero;
            tapRect.anchorMax = Vector2.one;
            tapRect.offsetMin = Vector2.zero;
            tapRect.offsetMax = Vector2.zero;

            Image tapImage = tapObject.GetComponent<Image>();
            tapImage.color = Color.clear;
            tapImage.raycastTarget = true;

            Button button = tapObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            return button;
        }

        private void CreateMousePortrait(RectTransform parent)
        {
            GameObject portraitObject = new GameObject("TutorialMousePortrait", typeof(RectTransform), typeof(Image));
            _mousePortrait = portraitObject;
            RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.SetParent(parent, false);
            portraitRect.anchorMin = new Vector2(0.015f, 0.045f);
            portraitRect.anchorMax = new Vector2(0.23f, 0.285f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            Image portraitImage = portraitObject.GetComponent<Image>();
            portraitImage.sprite = BlockBlastSpriteLibrary.MouseSprite;
            portraitImage.color = Color.white;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
        }

        private void CreateSpeechBubbleTail(RectTransform panelRect)
        {
            GameObject tailObject = new GameObject("TutorialSpeechBubbleTail", typeof(RectTransform), typeof(Image), typeof(Outline));
            RectTransform tailRect = tailObject.GetComponent<RectTransform>();
            tailRect.SetParent(panelRect, false);
            tailRect.anchorMin = new Vector2(0f, 0.43f);
            tailRect.anchorMax = new Vector2(0f, 0.43f);
            tailRect.anchoredPosition = new Vector2(-4f, 0f);
            tailRect.sizeDelta = new Vector2(42f, 42f);
            tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image tailImage = tailObject.GetComponent<Image>();
            tailImage.color = panelColor;
            tailImage.raycastTarget = false;

            Outline tailOutline = tailObject.GetComponent<Outline>();
            tailOutline.effectColor = panelOutlineColor;
            tailOutline.effectDistance = new Vector2(3f, -3f);
        }

        private TextMeshProUGUI CreateText(
            RectTransform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (messageText != null && messageText.font != null)
                text.font = messageText.font;

            if (messageText != null && messageText.fontSharedMaterial != null)
                text.fontSharedMaterial = messageText.fontSharedMaterial;

            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12f, fontSize * 0.55f);
            text.fontSizeMax = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private Button CreateButton(
            RectTransform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(1f, 0.98f, 0.9f, 1f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = panelOutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);

            TextMeshProUGUI text = CreateText(rectTransform, $"{objectName}LabelTxt", Vector2.zero, Vector2.one, 36f, titleColor, TextAlignmentOptions.Center);
            text.text = label;

            return buttonObject.GetComponent<Button>();
        }

        private void CreateGuideView(RectTransform rootRect)
        {
            GameObject guideRoot = new GameObject("TutorialGuide", typeof(RectTransform), typeof(CanvasGroup));
            _guideRootRect = guideRoot.GetComponent<RectTransform>();
            _guideRootRect.SetParent(rootRect, false);
            _guideRootRect.anchorMin = Vector2.zero;
            _guideRootRect.anchorMax = Vector2.one;
            _guideRootRect.offsetMin = Vector2.zero;
            _guideRootRect.offsetMax = Vector2.zero;

            CanvasGroup canvasGroup = guideRoot.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            _guideShaft = CreateGuideImage(_guideRootRect, "TutorialSwipeArrowShaft", guideColor);
            _guideShaft.rectTransform.pivot = new Vector2(0f, 0.5f);

            _guideHeadText = CreateGuideText(_guideRootRect, "TutorialSwipeArrowHead", ">", 72f, guideColor);
            _guidePointer = CreateGuideImage(_guideRootRect, "TutorialSwipePointer", Color.white);
            _guidePointer.sprite = GetGuidePointerSprite();
            _guidePointer.preserveAspect = true;
            _guidePointer.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _guidePointer.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _guidePointer.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _guidePointer.rectTransform.sizeDelta = Vector2.one * guidePointerSize;

            HideGuide();
        }

        private Image CreateGuideImage(RectTransform parent, string objectName, Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Outline));
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = Vector2.one * 40f;

            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            Outline outline = imageObject.GetComponent<Outline>();
            outline.effectColor = guideShadowColor;
            outline.effectDistance = new Vector2(3f, -3f);
            return image;
        }

        private TextMeshProUGUI CreateGuideText(RectTransform parent, string objectName, string value, float fontSize, Color color)
        {
            TextMeshProUGUI text = CreateText(parent, objectName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), fontSize, color, TextAlignmentOptions.Center);
            text.text = value;
            text.rectTransform.sizeDelta = Vector2.one * fontSize;
            text.raycastTarget = false;
            return text;
        }

        private void RefreshGuide()
        {
            HideGuide();

            if (!_isRunning || !_isMissionActive || _rootRect == null)
                return;

            TutorialStep step = CurrentStep;

            if (step.Kind == StepKind.WaitForShift)
            {
                ShowSwipeGuide(step.Direction);
                return;
            }

            if (step.Kind == StepKind.WaitForPiecePlaced ||
                step.Kind == StepKind.WaitForMatchingLineClear ||
                step.Kind == StepKind.WaitForIndividualSplit)
                ShowPlacementGuide();
        }

        private void HideGuide()
        {
            _guideVisible = false;

            if (_guideRootRect != null)
                _guideRootRect.gameObject.SetActive(false);

            for (int i = 0; i < _focusBoxes.Count; i++)
            {
                if (_focusBoxes[i] != null && _focusBoxes[i].IsAlive)
                    _focusBoxes[i].SetActive(false);
            }
        }

        private void ShowSwipeGuide(Vector2Int direction)
        {
            if (_mouse == null || _blockField == null)
                return;

            if (!_mouse.TryGetMoveGuideWorldPositions(direction, _blockField, out Vector3 fromWorld, out Vector3 toWorld))
                return;

            if (!TryWorldToCanvasPoint(fromWorld, out Vector2 fromLocal) ||
                !TryWorldToCanvasPoint(toWorld, out Vector2 toLocal))
                return;

            SetGuideArrow(fromLocal, toLocal);
            ShowFocusBox(0, fromWorld, _blockField.CellSize * 1.55f);
            ShowFocusBox(1, toWorld, _blockField.CellSize * 1.75f);
        }

        private void ShowPlacementGuide()
        {
            if (!TryFindPlacementGuide(out Vector3 pieceWorldPosition, out Vector3 targetWorldPosition))
                return;

            if (!TryWorldToCanvasPoint(pieceWorldPosition, out Vector2 pieceLocal) ||
                !TryWorldToCanvasPoint(targetWorldPosition, out Vector2 targetLocal))
                return;

            SetGuideArrow(pieceLocal, targetLocal);

            for (int i = 0; i < _placementTargetPositions.Count; i++)
                ShowFocusBox(i, _placementTargetPositions[i], _blockField.CellSize * 0.96f);
        }

        private bool TryFindPlacementGuide(out Vector3 pieceWorldPosition, out Vector3 targetWorldPosition)
        {
            pieceWorldPosition = Vector3.zero;
            targetWorldPosition = Vector3.zero;
            _placementTargetPositions.Clear();

            if (_blockField == null || _pieces == null)
                return false;

            if (CurrentStep.Kind == StepKind.WaitForMatchingLineClear &&
                TryFindLinePlacementGuide(false, out pieceWorldPosition, out targetWorldPosition))
                return true;

            if (CurrentStep.Kind == StepKind.WaitForIndividualSplit &&
                TryFindLinePlacementGuide(true, out pieceWorldPosition, out targetWorldPosition))
                return true;

            foreach (BlockPiece piece in _pieces)
            {
                if (piece == null || piece.IsPlaced || !piece.gameObject.activeInHierarchy)
                    continue;

                for (int y = _blockField.Height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < _blockField.Width; x++)
                    {
                        Vector2Int anchor = new Vector2Int(x, y);

                        if (!_blockField.CanInstall(piece, anchor))
                            continue;

                        pieceWorldPosition = piece.transform.position;
                        Vector3 total = Vector3.zero;

                        foreach (Vector2Int cell in piece.Cells)
                        {
                            Vector3 cellWorldPosition = _blockField.GetWorldPosition(anchor + cell);
                            _placementTargetPositions.Add(cellWorldPosition);
                            total += cellWorldPosition;
                        }

                        targetWorldPosition = total / Mathf.Max(1, _placementTargetPositions.Count);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryFindLinePlacementGuide(
            bool requirePartialPiece,
            out Vector3 pieceWorldPosition,
            out Vector3 targetWorldPosition)
        {
            pieceWorldPosition = Vector3.zero;
            targetWorldPosition = Vector3.zero;

            foreach (BlockPiece piece in _pieces)
            {
                if (piece == null || piece.IsPlaced || !piece.gameObject.activeInHierarchy)
                    continue;

                for (int y = _blockField.Height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < _blockField.Width; x++)
                    {
                        Vector2Int anchor = new Vector2Int(x, y);

                        if (!_blockField.CanInstall(piece, anchor))
                            continue;

                        if (!TryFindMatchingLine(piece, anchor, requirePartialPiece, out List<Vector2Int> linePoints))
                            continue;

                        pieceWorldPosition = piece.transform.position;
                        Vector3 total = Vector3.zero;
                        _placementTargetPositions.Clear();

                        foreach (Vector2Int cell in piece.Cells)
                        {
                            Vector3 cellWorldPosition = _blockField.GetWorldPosition(anchor + cell);
                            _placementTargetPositions.Add(cellWorldPosition);
                            total += cellWorldPosition;
                        }

                        foreach (Vector2Int point in linePoints)
                        {
                            Vector3 lineWorldPosition = _blockField.GetWorldPosition(point);
                            _placementTargetPositions.Add(lineWorldPosition);
                        }

                        targetWorldPosition = total / Mathf.Max(1, piece.Cells.Count);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryFindMatchingLine(
            BlockPiece piece,
            Vector2Int anchor,
            bool requirePartialPiece,
            out List<Vector2Int> matchingLine)
        {
            matchingLine = null;
            Sprite targetSprite = piece.BlockSprite;

            for (int y = 0; y < _blockField.Height; y++)
            {
                if (IsMatchingLine(piece, anchor, targetSprite, Vector2Int.right, new Vector2Int(0, y), _blockField.Width, requirePartialPiece))
                {
                    matchingLine = new List<Vector2Int>(_blockField.Width);
                    for (int x = 0; x < _blockField.Width; x++)
                        matchingLine.Add(new Vector2Int(x, y));
                    return true;
                }
            }

            for (int x = 0; x < _blockField.Width; x++)
            {
                if (IsMatchingLine(piece, anchor, targetSprite, Vector2Int.up, new Vector2Int(x, 0), _blockField.Height, requirePartialPiece))
                {
                    matchingLine = new List<Vector2Int>(_blockField.Height);
                    for (int y = 0; y < _blockField.Height; y++)
                        matchingLine.Add(new Vector2Int(x, y));
                    return true;
                }
            }

            return false;
        }

        private bool IsMatchingLine(
            BlockPiece piece,
            Vector2Int anchor,
            Sprite targetSprite,
            Vector2Int step,
            Vector2Int start,
            int length,
            bool requirePartialPiece)
        {
            bool hasPieceCellOnLine = false;
            bool hasPieceCellOffLine = false;

            foreach (Vector2Int pieceCell in piece.Cells)
            {
                Vector2Int point = anchor + pieceCell;
                bool isOnLine = IsPointOnLine(point, start, step, length);

                if (isOnLine)
                    hasPieceCellOnLine = true;
                else
                    hasPieceCellOffLine = true;
            }

            if (!hasPieceCellOnLine || (requirePartialPiece && !hasPieceCellOffLine))
                return false;

            for (int i = 0; i < length; i++)
            {
                Vector2Int point = start + step * i;
                if (!_blockField.TryGetField(point, out _Code.Field.Field field))
                    return false;

                if (TryGetPieceCellAtPoint(piece, anchor, point, out _))
                {
                    if (piece.BlockSprite != targetSprite)
                        return false;
                    continue;
                }

                if (field.IsEmpty || field.IsStageCheese || field.CurrentSprite != targetSprite)
                    return false;
            }

            return true;
        }

        private static bool TryGetPieceCellAtPoint(BlockPiece piece, Vector2Int anchor, Vector2Int point, out Vector2Int pieceCell)
        {
            for (int i = 0; i < piece.Cells.Count; i++)
            {
                if (anchor + piece.Cells[i] != point)
                    continue;

                pieceCell = piece.Cells[i];
                return true;
            }

            pieceCell = default;
            return false;
        }

        private static bool IsPointOnLine(Vector2Int point, Vector2Int start, Vector2Int step, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (start + step * i == point)
                    return true;
            }

            return false;
        }

        private void SetGuideArrow(Vector2 fromLocal, Vector2 toLocal)
        {
            if (_guideRootRect == null || _guideShaft == null || _guideHeadText == null || _guidePointer == null)
                return;

            Vector2 delta = toLocal - fromLocal;
            float length = delta.magnitude;

            if (length < 24f)
                return;

            Vector2 direction = delta.normalized;
            _guideStartLocal = fromLocal + direction * Mathf.Min(52f, length * 0.22f);
            _guideEndLocal = toLocal - direction * Mathf.Min(46f, length * 0.18f);
            Vector2 guideDelta = _guideEndLocal - _guideStartLocal;
            float guideLength = guideDelta.magnitude;
            float angle = Mathf.Atan2(guideDelta.y, guideDelta.x) * Mathf.Rad2Deg;

            _guideRootRect.gameObject.SetActive(true);
            _guideVisible = true;

            RectTransform shaftRect = _guideShaft.rectTransform;
            shaftRect.anchoredPosition = _guideStartLocal;
            shaftRect.sizeDelta = new Vector2(Mathf.Max(24f, guideLength - 34f), guideLineWidth);
            shaftRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            RectTransform headRect = _guideHeadText.rectTransform;
            headRect.anchoredPosition = _guideEndLocal;
            headRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            _guidePointer.rectTransform.anchoredPosition = _guideStartLocal;
        }

        private void AnimateGuide()
        {
            if (!_guideVisible || _guidePointer == null)
                return;

            // Play a one-way swipe, then restart at the beginning for a clear repeating gesture.
            float travel = Mathf.Repeat(Time.unscaledTime * guideAnimationSpeed, 1f);
            float easedTravel = Mathf.SmoothStep(0f, 1f, travel);
            RectTransform pointerRect = _guidePointer.rectTransform;
            pointerRect.anchoredPosition = Vector2.Lerp(_guideStartLocal, _guideEndLocal, easedTravel);

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.08f;
            pointerRect.localScale = Vector3.one * pulse;
        }

        private void ShowFocusBox(int index, Vector3 worldPosition, float worldSize)
        {
            if (!TryWorldToCanvasPoint(worldPosition, out Vector2 localPosition))
                return;

            FocusBox focusBox = GetFocusBox(index);
            Vector2 size = WorldSizeToCanvasSize(worldPosition, worldSize) + Vector2.one * guideFocusPadding;
            focusBox.Set(localPosition, size, guideColor, guideFocusThickness);
            focusBox.SetActive(true);
        }

        private FocusBox GetFocusBox(int index)
        {
            while (_focusBoxes.Count <= index)
                _focusBoxes.Add(new FocusBox(_guideRootRect, $"TutorialFocusBox{_focusBoxes.Count}"));

            return _focusBoxes[index];
        }

        private bool TryWorldToCanvasPoint(Vector3 worldPosition, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;

            if (_rootRect == null)
                return false;

            if (_worldCamera == null)
                _worldCamera = Camera.main;

            if (_worldCamera == null)
                return false;

            Camera canvasCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera != null ? targetCanvas.worldCamera : _worldCamera
                : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_worldCamera, worldPosition);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screenPoint, canvasCamera, out localPoint);
        }

        private Vector2 WorldSizeToCanvasSize(Vector3 worldPosition, float worldSize)
        {
            if (!TryWorldToCanvasPoint(worldPosition, out Vector2 center) ||
                !TryWorldToCanvasPoint(worldPosition + Vector3.right * worldSize, out Vector2 right) ||
                !TryWorldToCanvasPoint(worldPosition + Vector3.up * worldSize, out Vector2 up))
                return Vector2.one * 72f;

            return new Vector2(
                Mathf.Max(32f, Mathf.Abs(right.x - center.x)),
                Mathf.Max(32f, Mathf.Abs(up.y - center.y)));
        }

        private Sprite GetGuidePointerSprite()
        {
            if (_guidePointerSprite != null)
                return _guidePointerSprite;

            _guidePointerSprite = Resources.Load<Sprite>("BlockBlast/fingerSprite");
            if (_guidePointerSprite == null)
                Debug.LogWarning("Tutorial swipe pointer sprite was not found at Resources/BlockBlast/fingerSprite.", this);

            return _guidePointerSprite;
        }

        private Sprite GetSpeechBubbleSprite()
        {
            if (_speechBubbleSprite != null)
                return _speechBubbleSprite;

            const int textureSize = 64;
            const float cornerRadius = 18f;
            _speechBubbleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "TutorialSpeechBubble",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float straightHalfSize = center.x - cornerRadius;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    Vector2 distanceFromCenter = new Vector2(
                        Mathf.Abs(x - center.x),
                        Mathf.Abs(y - center.y));
                    Vector2 cornerDistance = new Vector2(
                        Mathf.Max(0f, distanceFromCenter.x - straightHalfSize),
                        Mathf.Max(0f, distanceFromCenter.y - straightHalfSize));
                    float alpha = Mathf.Clamp01(cornerRadius + 0.5f - cornerDistance.magnitude);
                    _speechBubbleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            _speechBubbleTexture.Apply();
            _speechBubbleSprite = Sprite.Create(
                _speechBubbleTexture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize,
                0,
                SpriteMeshType.FullRect,
                Vector4.one * cornerRadius);
            _speechBubbleSprite.name = "TutorialSpeechBubble";
            return _speechBubbleSprite;
        }

        private void BuildSteps()
        {
            if (_steps.Count > 0)
                return;

            _steps.Add(new TutorialStep(
                "고양이 배치",
                "먼저 아래 <b><color=#8A3D25>고양이 블럭</color></b>을 잡고 <b><color=#C47A12>노란 칸</color></b>에 맞춰 놓아보세요.",
                "고양이 놓기",
                StepKind.WaitForPiecePlaced,
                Vector2Int.zero));

            _steps.Add(new TutorialStep(
                "쥐 이동하기",
                "이제 화면을 <b><color=#8A3D25>스와이프</color></b>하면 쥐가 쥐구멍 사이를 이동하고, 고양이 블럭들도 모양 그대로 같은 방향으로 밀려요. <b><color=#C47A12>스와이프로 줄을 지우면 점수가 2배!</color></b>\n<b><color=#8A3D25>놓을 곳이 없으면 먼저 스와이프로 공간을 만들고, 그래도 놓을 곳이 없으면 게임 오버예요.</color></b>",
                "쥐를 움직여볼까요?",
                StepKind.Message,
                Vector2Int.zero));

            _steps.Add(new TutorialStep(
                "오른쪽으로 밀기",
                "노란 화살표처럼 <b><color=#C47A12>오른쪽으로 스와이프</color></b>해 쥐를 이동시켜봐요.",
                "오른쪽으로 밀기",
                StepKind.WaitForShift,
                Vector2Int.right));

            _steps.Add(new TutorialStep(
                "아래로 내려가기",
                "이번에는 <b><color=#C47A12>아래로 스와이프</color></b>해서 아래 쥐구멍으로 이동해요.",
                "아래로 밀기",
                StepKind.WaitForShift,
                Vector2Int.down));

            _steps.Add(new TutorialStep(
                "왼쪽으로 밀기",
                "<b><color=#C47A12>왼쪽으로 스와이프</color></b>하면 고양이 블럭들이 같이 정리돼요.",
                "왼쪽으로 밀기",
                StepKind.WaitForShift,
                Vector2Int.left));

            _steps.Add(new TutorialStep(
                "다시 위로",
                "마지막으로 <b><color=#C47A12>위로 스와이프</color></b>해서 쥐를 위쪽 쥐구멍으로 보내요.",
                "위로 밀기",
                StepKind.WaitForShift,
                Vector2Int.up));

            _steps.Add(new TutorialStep(
                "고양이 성장",
                "<b><color=#8A3D25>같은 고양이로만 한 줄</color></b>을 지우면 그 색 고양이가 <b><color=#C47A12>자라요!</color></b> 같은 고양이를 모아 한 줄을 완성해볼게요.",
                "고양이를 키워봐요!",
                StepKind.WaitForMatchingLineClear,
                Vector2Int.zero));

            _steps.Add(new TutorialStep(
                "개별 판정",
                "블럭의 일부만 줄에서 지워지면 남은 고양이 블럭은 하나씩 따로 판정돼요.\n<b>원래는 블럭 단위로 움직이지만, 단일 객체가 되면 혼자 움직여요.\n단일 객체가 되면 아웃라인이 생겨 쉽게 구분할 수 있어요.</b>\n이제 가로나 세로줄을 채워 점수를 얻어보세요.",
                "블럭 일부를 지워봐요!",
                StepKind.WaitForIndividualSplit,
                Vector2Int.zero));
        }

        private static Vector2Int NormalizeDirection(Vector2Int direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                return direction.x > 0 ? Vector2Int.right : Vector2Int.left;

            return direction.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        private static string DirectionToKorean(Vector2Int direction)
        {
            if (direction == Vector2Int.right)
                return "오른쪽";

            if (direction == Vector2Int.left)
                return "왼쪽";

            if (direction == Vector2Int.up)
                return "위";

            return "아래";
        }

        private readonly struct TutorialStep
        {
            public TutorialStep(string title, string body, string shortMessage, StepKind kind, Vector2Int direction)
            {
                Title = title;
                Body = body;
                ShortMessage = shortMessage;
                Kind = kind;
                Direction = direction;
            }

            public string Title { get; }
            public string Body { get; }
            public string ShortMessage { get; }
            public StepKind Kind { get; }
            public Vector2Int Direction { get; }
        }

        private sealed class FocusBox
        {
            private readonly RectTransform _rectTransform;
            private readonly Image[] _edges;

            public bool IsAlive => _rectTransform != null;

            public FocusBox(RectTransform parent, string objectName)
            {
                GameObject root = new GameObject(objectName, typeof(RectTransform));
                _rectTransform = root.GetComponent<RectTransform>();
                _rectTransform.SetParent(parent, false);
                _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

                _edges = new[]
                {
                    CreateEdge(_rectTransform, "Top"),
                    CreateEdge(_rectTransform, "Bottom"),
                    CreateEdge(_rectTransform, "Left"),
                    CreateEdge(_rectTransform, "Right")
                };

                SetActive(false);
            }

            public void Set(Vector2 center, Vector2 size, Color color, float thickness)
            {
                if (!IsAlive)
                    return;

                _rectTransform.anchoredPosition = center;
                _rectTransform.sizeDelta = size;

                ConfigureHorizontal(_edges[0].rectTransform, true, thickness);
                ConfigureHorizontal(_edges[1].rectTransform, false, thickness);
                ConfigureVertical(_edges[2].rectTransform, false, thickness);
                ConfigureVertical(_edges[3].rectTransform, true, thickness);

                foreach (Image edge in _edges)
                {
                    if (edge != null)
                        edge.color = color;
                }
            }

            public void SetActive(bool active)
            {
                if (!IsAlive)
                    return;

                _rectTransform.gameObject.SetActive(active);
            }

            private static Image CreateEdge(RectTransform parent, string objectName)
            {
                GameObject edgeObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
                RectTransform rectTransform = edgeObject.GetComponent<RectTransform>();
                rectTransform.SetParent(parent, false);

                Image image = edgeObject.GetComponent<Image>();
                image.raycastTarget = false;
                return image;
            }

            private static void ConfigureHorizontal(RectTransform rectTransform, bool isTop, float thickness)
            {
                rectTransform.anchorMin = new Vector2(0f, isTop ? 1f : 0f);
                rectTransform.anchorMax = new Vector2(1f, isTop ? 1f : 0f);
                rectTransform.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.sizeDelta = new Vector2(0f, thickness);
            }

            private static void ConfigureVertical(RectTransform rectTransform, bool isRight, float thickness)
            {
                rectTransform.anchorMin = new Vector2(isRight ? 1f : 0f, 0f);
                rectTransform.anchorMax = new Vector2(isRight ? 1f : 0f, 1f);
                rectTransform.pivot = new Vector2(isRight ? 1f : 0f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.sizeDelta = new Vector2(thickness, 0f);
            }
        }
    }
}
