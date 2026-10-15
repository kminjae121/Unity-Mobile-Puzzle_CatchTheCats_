using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Code.Block
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class BlockPiece : Blcok
    {
        [SerializeField] private BlockCellView[] _cellViews;
        [SerializeField] private Camera _mainCamera;
        [SerializeField, Min(0.1f)] private float _cellSize = 0.72f;
        [SerializeField, Min(0.1f)] private float _slotCellSize = 0.72f;
        [SerializeField, Min(0.1f)] private float _slotCellScaleMultiplier = 0.9f;
        [SerializeField, Min(0.1f)] private float _dragCellScaleMultiplier = 1.14f;
        [SerializeField, Min(1f)] private float _pickupColliderSizeMultiplier = 2.5f;
        [SerializeField] private float _dragLift = 0.45f;
        [SerializeField] private int _defaultSortingOrder = 3;
        [SerializeField] private int _dragSortingOrder = 20;

        private const int PlacementHighlightTextureSize = 32;
        private const int PlacementHighlightBorderSize = 4;
        private const int PlacementBlockedSortingOrder = 30000;
        private const int PlacementBlockedHighlightSortingOrder = 30001;
        private const int PlacementBlockedBurstSortingOrder = 30002;
        private const int HeldPieceSortingOrder = 30000;
        private const float PlacementBlockedMoveDuration = 2f;
        private const float PlacementBlockedPauseDuration = 0.5f;
        private const float PlacementBlockedShakeDuration = 1.3f;
        private const float PlacementBlockedPopDuration = 0.28f;
        private const float PlacementBlockedBlockScale = 1.35f;
        private const float PlacementBlockedBurstFallbackScale = 3.8f;
        private static readonly Color PlacementHighlightColor = new Color(1f, 0.2f, 0.12f, 1f);
        private static Sprite _placementHighlightSprite;
        private static Sprite _placementBurstSprite;

        private const int MousePointerId = -1;

        private static BlockPiece _activePiece;

        private readonly List<Vector2Int> _cells = new List<Vector2Int>(9);
        private readonly List<SpriteRenderer> _placementHighlightRenderers = new List<SpriteRenderer>(9);
        private SpriteRenderer _placementBurstRenderer;
        private BoxCollider2D _touchCollider;
        private Sprite _catSprite;
        private Vector2 _visualCenter;
        private Vector3 _slotPosition;
        private Vector3 _dragOffset;
        private Vector3 _releaseAnchorWorldPosition;
        private Func<BlockPiece, bool> _canBeginDrag;
        private int _activePointerId = MousePointerId;
        private bool _isDragging;
        private bool _isPlaced = true;
        private bool _hasReleaseAnchorWorldPosition;
        private bool _showPlacementBlockedHighlight;
        private bool _placementHighlightUsesDragView;
        private bool _isPlacementBlockedAnimating;

        public event Action<BlockPiece> Released;

        public override IReadOnlyList<Vector2Int> Cells => _cells;
        public override Sprite BlockSprite => _catSprite != null ? _catSprite : BlockBlastSpriteLibrary.CatBlockSprite;
        public bool IsPlaced => _isPlaced;
        public float CellSize => _cellSize;
        public static BlockPiece ActivePiece => _activePiece;
        public static bool IsAnyDragging => _activePiece != null;
        public const float PlacementBlockedAnimationDuration =
            PlacementBlockedMoveDuration +
            PlacementBlockedPauseDuration +
            PlacementBlockedShakeDuration +
            PlacementBlockedPopDuration;

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_cellViews == null || _cellViews.Length == 0)
                _cellViews = GetComponentsInChildren<BlockCellView>(true);

            _touchCollider = GetComponent<BoxCollider2D>();
            _slotPosition = transform.position;
        }

        private void Update()
        {
            if (_isPlaced)
                return;

            if (Input.touchCount > 0)
            {
                HandleTouchInput();
                return;
            }

            if (_isDragging && _activePointerId != MousePointerId)
            {
                EndDrag();
                return;
            }

            HandleMouseInput();
        }

        private void OnDisable()
        {
            if (_isDragging)
                _isDragging = false;

            _hasReleaseAnchorWorldPosition = false;
            ClearActivePointer();
        }

        public void CaptureSlotPosition()
        {
            _slotPosition = transform.position;
        }

        public void SetCanBeginDrag(Func<BlockPiece, bool> canBeginDrag)
        {
            _canBeginDrag = canBeginDrag;
        }

        public void Configure(BlockShape shape, Color color)
        {
            Configure(shape, color, BlockBlastSpriteLibrary.GetRandomCatBlockSprite());
        }

        public void Configure(BlockShape shape, Color color, Sprite catSprite)
        {
            gameObject.SetActive(true);

            _cells.Clear();
            for (int i = 0; i < shape.Cells.Count; i++)
                _cells.Add(shape.Cells[i]);

            _visualCenter = shape.VisualCenter;
            _catSprite = catSprite != null ? catSprite : BlockBlastSpriteLibrary.CatBlockSprite;
            _isPlaced = false;
            _isDragging = false;
            _hasReleaseAnchorWorldPosition = false;
            _showPlacementBlockedHighlight = false;
            _placementHighlightUsesDragView = false;
            _isPlacementBlockedAnimating = false;
            transform.position = _slotPosition;
            ApplySlotView();
        }

        public Vector3 GetAnchorWorldPosition()
        {
            return transform.position - GetAnchorOffset();
        }

        public Vector3 GetReleaseAnchorWorldPosition()
        {
            return _hasReleaseAnchorWorldPosition ? _releaseAnchorWorldPosition : GetAnchorWorldPosition();
        }

        public void SnapTo(Vector3 anchorWorldPosition)
        {
            transform.position = anchorWorldPosition + GetAnchorOffset();
        }

        public void ReturnToSlot()
        {
            _isDragging = false;
            _hasReleaseAnchorWorldPosition = false;
            ClearActivePointer();
            transform.position = _slotPosition;
            ApplySlotView();
            SetSortingOrder(_defaultSortingOrder);
        }

        public void SetPlacementBlockedHighlight(bool enabled)
        {
            _showPlacementBlockedHighlight = enabled && !_isPlaced;

            if (_showPlacementBlockedHighlight)
                SetSortingOrder(PlacementBlockedSortingOrder);
            else if (!_isDragging)
                SetSortingOrder(_defaultSortingOrder);

            UpdatePlacementHighlightView();
        }

        public IEnumerator PlayPlacementBlockedAnimation(Vector3 targetWorldPosition, float delay)
        {
            if (_isPlaced)
                yield break;

            _isPlacementBlockedAnimating = true;
            SetPlacementBlockedHighlight(true);
            SetSortingOrder(PlacementBlockedSortingOrder);

            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            _placementHighlightUsesDragView = true;
            ApplyDragView();
            Vector3 startPosition = transform.position;
            float elapsed = 0f;
            while (elapsed < PlacementBlockedMoveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / PlacementBlockedMoveDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(startPosition, targetWorldPosition, eased);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, PlacementBlockedBlockScale, eased);
                yield return null;
            }

            transform.position = targetWorldPosition;
            transform.localScale = Vector3.one * PlacementBlockedBlockScale;

            if (PlacementBlockedPauseDuration > 0f)
                yield return new WaitForSecondsRealtime(PlacementBlockedPauseDuration);

            elapsed = 0f;
            while (elapsed < PlacementBlockedShakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / PlacementBlockedShakeDuration);
                float fade = 1f - t * 0.35f;
                float shakeX = Mathf.Sin(elapsed * 48f) * 0.06f * fade;
                float shakeY = Mathf.Cos(elapsed * 55f) * 0.035f * fade;
                float shakeAngle = Mathf.Sin(elapsed * 52f) * 7f * fade;
                transform.position = targetWorldPosition + new Vector3(shakeX, shakeY, 0f);
                transform.localRotation = Quaternion.Euler(0f, 0f, shakeAngle);
                yield return null;
            }

            transform.position = targetWorldPosition;
            transform.localRotation = Quaternion.identity;
            SetCellViewsVisible(false);
            SetPlacementHighlightRenderersVisible(false);

            EnsurePlacementBurstRenderer();
            _placementBurstRenderer.transform.localScale = Vector3.one * 0.15f;
            _placementBurstRenderer.color = PlacementHighlightColor;
            PositionBurstAtCameraCenter();
            _placementBurstRenderer.gameObject.SetActive(true);
            Vector3 burstTargetScale = GetScreenCoverBurstScale();

            elapsed = 0f;
            while (elapsed < PlacementBlockedPopDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / PlacementBlockedPopDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                _placementBurstRenderer.transform.localScale = Vector3.Lerp(
                    Vector3.one * 0.15f,
                    burstTargetScale,
                    eased);
                Color burstColor = PlacementHighlightColor;
                burstColor.a = Mathf.Lerp(1f, 0f, eased);
                _placementBurstRenderer.color = burstColor;
                yield return null;
            }

            _placementBurstRenderer.gameObject.SetActive(false);
            _isPlacementBlockedAnimating = false;
            gameObject.SetActive(false);
        }

        public void MarkPlaced()
        {
            _isPlaced = true;
            _showPlacementBlockedHighlight = false;
            _hasReleaseAnchorWorldPosition = false;
            ClearActivePointer();
            SetPlacementHighlightRenderersVisible(false);
            gameObject.SetActive(false);
        }

        private void HandleTouchInput()
        {
            if (_isDragging)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);

                    if (touch.fingerId != _activePointerId)
                        continue;

                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        EndDrag(touch.position);
                    else
                        MoveDrag(touch.position);

                    return;
                }

                EndDrag();
                return;
            }

            if (_activePiece != null)
                return;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began && IsPointerInside(touch.position))
                {
                    BeginDrag(touch.fingerId, touch.position);
                    return;
                }
            }
        }

        private void HandleMouseInput()
        {
            if (_isDragging && _activePointerId == MousePointerId)
            {
                if (Input.GetMouseButtonUp(0))
                    EndDrag(Input.mousePosition);
                else if (Input.GetMouseButton(0))
                    MoveDrag(Input.mousePosition);

                return;
            }

            if (_activePiece != null || !Input.GetMouseButtonDown(0) || !IsPointerInside(Input.mousePosition))
                return;

            BeginDrag(MousePointerId, Input.mousePosition);
        }

        private void BeginDrag(int pointerId, Vector3 screenPosition)
        {
            if (_canBeginDrag != null && !_canBeginDrag(this))
                return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            _activePiece = this;
            _activePointerId = pointerId;
            ApplyDragView();
            _dragOffset = transform.position - GetPointerWorldPosition(screenPosition);
            _hasReleaseAnchorWorldPosition = false;
            _isDragging = true;
            SetSortingOrder(Mathf.Max(_dragSortingOrder, HeldPieceSortingOrder));
            MoveDrag(screenPosition);
        }

        private void MoveDrag(Vector3 screenPosition)
        {
            Vector3 releasePosition = GetPointerWorldPosition(screenPosition) + _dragOffset;
            _releaseAnchorWorldPosition = releasePosition - GetAnchorOffset();
            _hasReleaseAnchorWorldPosition = true;
            transform.position = releasePosition + Vector3.up * _dragLift;
        }

        private void EndDrag(Vector3 screenPosition)
        {
            MoveDrag(screenPosition);
            EndDrag();
        }

        private void EndDrag()
        {
            _isDragging = false;
            ClearActivePointer();
            SetSortingOrder(_defaultSortingOrder);
            Released?.Invoke(this);
        }

        private bool IsPointerInside(Vector3 screenPosition)
        {
            if (_touchCollider == null)
                return false;

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera == null)
                return false;

            return _touchCollider.OverlapPoint(GetPointerWorldPosition(screenPosition));
        }

        private Vector3 GetPointerWorldPosition(Vector3 screenPosition)
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            screenPosition.z = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);
            Vector3 worldPosition = _mainCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = transform.position.z;
            return worldPosition;
        }

        private void ClearActivePointer()
        {
            if (_activePiece == this)
                _activePiece = null;

            _activePointerId = MousePointerId;
        }

        private void UpdateCellViews(float displayCellSize, float cellScaleMultiplier)
        {
            for (int i = 0; i < _cellViews.Length; i++)
            {
                bool isActive = i < _cells.Count;
                BlockCellView cellView = _cellViews[i];
                cellView.SetVisible(isActive);

                if (!isActive)
                    continue;

                Vector2Int cell = _cells[i];
                cellView.transform.localPosition = new Vector3((cell.x - _visualCenter.x) * displayCellSize, (cell.y - _visualCenter.y) * displayCellSize, 0f);
                cellView.transform.localScale = Vector3.one * (displayCellSize * cellScaleMultiplier);
                cellView.SetSprite(BlockSprite);
                cellView.SetSortingOrder(_defaultSortingOrder);
            }
        }

        private void UpdateCollider(float displayCellSize)
        {
            if (_touchCollider == null || _cells.Count == 0)
                return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            foreach (Vector2Int cell in _cells)
            {
                Vector2 localPosition = new Vector2((cell.x - _visualCenter.x) * displayCellSize, (cell.y - _visualCenter.y) * displayCellSize);
                minX = Mathf.Min(minX, localPosition.x);
                maxX = Mathf.Max(maxX, localPosition.x);
                minY = Mathf.Min(minY, localPosition.y);
                maxY = Mathf.Max(maxY, localPosition.y);
            }

            Vector2 baseSize = new Vector2(maxX - minX + displayCellSize, maxY - minY + displayCellSize);

            _touchCollider.offset = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            _touchCollider.size = baseSize * Mathf.Max(1f, _pickupColliderSizeMultiplier);
            _touchCollider.isTrigger = true;
        }

        private void SetSortingOrder(int order)
        {
            foreach (BlockCellView cellView in _cellViews)
                cellView.SetSortingOrder(order);
        }

        private void ApplySlotView()
        {
            transform.localScale = Vector3.one;
            _placementHighlightUsesDragView = false;
            UpdateCellViews(_slotCellSize, _slotCellScaleMultiplier);
            UpdateCollider(_slotCellSize);
            UpdatePlacementHighlightView();
        }

        private void ApplyDragView()
        {
            transform.localScale = Vector3.one;
            UpdateCellViews(_cellSize, _dragCellScaleMultiplier);
            UpdateCollider(_cellSize);
            if (_showPlacementBlockedHighlight)
                UpdatePlacementHighlightView();
            else
                SetPlacementHighlightRenderersVisible(false);

            if (_isPlacementBlockedAnimating)
                SetSortingOrder(PlacementBlockedSortingOrder);
        }

        private void UpdatePlacementHighlightView()
        {
            EnsurePlacementHighlightRenderers(_cells.Count);

            float displayCellSize = _placementHighlightUsesDragView ? _cellSize : _slotCellSize;
            float cellScaleMultiplier = _placementHighlightUsesDragView
                ? _dragCellScaleMultiplier
                : _slotCellScaleMultiplier;

            for (int i = 0; i < _placementHighlightRenderers.Count; i++)
            {
                bool isActive = _showPlacementBlockedHighlight && i < _cells.Count;
                SpriteRenderer renderer = _placementHighlightRenderers[i];
                renderer.gameObject.SetActive(isActive);

                if (!isActive)
                    continue;

                Vector2Int cell = _cells[i];
                renderer.transform.localPosition = new Vector3(
                    (cell.x - _visualCenter.x) * displayCellSize,
                    (cell.y - _visualCenter.y) * displayCellSize,
                    -0.02f);
                renderer.transform.localScale = Vector3.one * (displayCellSize * cellScaleMultiplier);
                renderer.sortingOrder = _showPlacementBlockedHighlight || _isPlacementBlockedAnimating
                    ? PlacementBlockedHighlightSortingOrder
                    : _defaultSortingOrder + 2;
            }
        }

        private void EnsurePlacementHighlightRenderers(int count)
        {
            if (_placementHighlightSprite == null)
                _placementHighlightSprite = CreatePlacementHighlightSprite();

            while (_placementHighlightRenderers.Count < count)
            {
                GameObject highlightObject = new GameObject($"PlacementBlockedHighlight_{_placementHighlightRenderers.Count + 1}");
                highlightObject.transform.SetParent(transform, false);

                SpriteRenderer renderer = highlightObject.AddComponent<SpriteRenderer>();
                renderer.sprite = _placementHighlightSprite;
                renderer.color = PlacementHighlightColor;
                renderer.sortingOrder = _defaultSortingOrder + 2;
                renderer.gameObject.SetActive(false);
                _placementHighlightRenderers.Add(renderer);
            }
        }

        private void SetPlacementHighlightRenderersVisible(bool visible)
        {
            foreach (SpriteRenderer renderer in _placementHighlightRenderers)
            {
                if (renderer != null)
                    renderer.gameObject.SetActive(visible);
            }
        }

        private void SetCellViewsVisible(bool visible)
        {
            foreach (BlockCellView cellView in _cellViews)
            {
                if (cellView != null)
                    cellView.SetVisible(visible);
            }
        }

        private void EnsurePlacementBurstRenderer()
        {
            if (_placementBurstRenderer != null)
                return;

            if (_placementBurstSprite == null)
                _placementBurstSprite = CreatePlacementBurstSprite();

            GameObject burstObject = new GameObject("PlacementBlockedBurst");
            burstObject.transform.SetParent(transform, false);
            _placementBurstRenderer = burstObject.AddComponent<SpriteRenderer>();
            _placementBurstRenderer.sprite = _placementBurstSprite;
            _placementBurstRenderer.sortingOrder = PlacementBlockedBurstSortingOrder;
            _placementBurstRenderer.gameObject.SetActive(false);
        }

        private void PositionBurstAtCameraCenter()
        {
            Camera targetCamera = _mainCamera != null ? _mainCamera : Camera.main;
            if (targetCamera == null || _placementBurstRenderer == null)
                return;

            Vector3 position = targetCamera.transform.position;
            position.z = transform.position.z;
            _placementBurstRenderer.transform.position = position;
        }

        private Vector3 GetScreenCoverBurstScale()
        {
            Camera targetCamera = _mainCamera != null ? _mainCamera : Camera.main;
            if (targetCamera == null || _placementBurstSprite == null)
                return Vector3.one * PlacementBlockedBurstFallbackScale;

            float screenHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 20f;
            float screenWidth = targetCamera.orthographic
                ? screenHeight * targetCamera.aspect
                : 20f;
            float screenDiagonal = Mathf.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight) * 1.15f;
            float spriteSize = Mathf.Max(
                _placementBurstSprite.bounds.size.x,
                _placementBurstSprite.bounds.size.y);
            float parentScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.01f);
            float localScale = screenDiagonal / Mathf.Max(spriteSize, 0.01f) / parentScale;
            return Vector3.one * localScale;
        }

        private static Sprite CreatePlacementHighlightSprite()
        {
            Texture2D texture = new Texture2D(
                PlacementHighlightTextureSize,
                PlacementHighlightTextureSize,
                TextureFormat.RGBA32,
                false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < PlacementHighlightTextureSize; y++)
            {
                for (int x = 0; x < PlacementHighlightTextureSize; x++)
                {
                    bool isBorder = x < PlacementHighlightBorderSize ||
                                    y < PlacementHighlightBorderSize ||
                                    x >= PlacementHighlightTextureSize - PlacementHighlightBorderSize ||
                                    y >= PlacementHighlightTextureSize - PlacementHighlightBorderSize;
                    texture.SetPixel(x, y, isBorder ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, PlacementHighlightTextureSize, PlacementHighlightTextureSize),
                new Vector2(0.5f, 0.5f),
                PlacementHighlightTextureSize);
        }

        private static Sprite CreatePlacementBurstSprite()
        {
            Texture2D texture = new Texture2D(
                PlacementHighlightTextureSize,
                PlacementHighlightTextureSize,
                TextureFormat.RGBA32,
                false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(PlacementHighlightTextureSize * 0.5f, PlacementHighlightTextureSize * 0.5f);
            for (int y = 0; y < PlacementHighlightTextureSize; y++)
            {
                for (int x = 0; x < PlacementHighlightTextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float angle = Mathf.Atan2(y - center.y, x - center.x);
                    float ray = Mathf.Abs(Mathf.Sin(angle * 4f));
                    bool isBurst = distance > 4f && distance < 14f && ray > 0.82f;
                    texture.SetPixel(x, y, isBurst ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, PlacementHighlightTextureSize, PlacementHighlightTextureSize),
                new Vector2(0.5f, 0.5f),
                PlacementHighlightTextureSize);
        }

        private Vector3 GetAnchorOffset()
        {
            return new Vector3(_visualCenter.x * _cellSize, _visualCenter.y * _cellSize, 0f);
        }
    }
}
