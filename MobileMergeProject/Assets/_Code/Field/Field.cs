using _Code.Block;
using DG.Tweening;
using UnityEngine;

namespace _Code.Field
{
    public class Field : MonoBehaviour
    {
        [field: SerializeField] public Vector2Int Point { get; private set; }
        [SerializeField] private SpriteRenderer _backgroundRenderer;
        [SerializeField] private SpriteRenderer _objectRenderer;
        [SerializeField, Min(0f)] private float _objectMoveDuration = 0.2f;
        [SerializeField] private Ease _objectMoveEase = Ease.Linear;

        private bool _isHaveObject;
        private GameObject _thisObject;
        private Color _currentColor = Color.white;
        private Sprite _currentSprite;
        private Sprite _visualSprite;
        private int _currentGroupId = -1;
        private bool _isStageCheese;
        private SpriteRenderer _objectRendererTemplate;
        private Vector3 _objectRendererHomeLocalPosition;
        private Quaternion _objectRendererHomeLocalRotation = Quaternion.identity;
        private Vector3 _objectRendererHomeLocalScale = Vector3.one;
        private bool _hasObjectRendererHomeLocalPosition;
        private Tween _objectMoveTween;

        public bool IsHaveObject => _isHaveObject;
        public bool IsEmpty => !_isHaveObject;
        public GameObject CurrentObject => _thisObject;
        public Color CurrentColor => _currentColor;
        public Sprite CurrentSprite => _currentSprite;
        public Sprite VisualSprite => _visualSprite;
        public int CurrentGroupId => _currentGroupId;
        public bool IsStageCheese => _isStageCheese;
        public SpriteRenderer ObjectRendererTemplate => _objectRendererTemplate != null ? _objectRendererTemplate : _objectRenderer;
        public Vector3 ObjectRendererWorldPosition => _objectRenderer != null ? _objectRenderer.transform.position : GetObjectRendererHomeWorldPosition();

        private void Awake()
        {
            if (_backgroundRenderer == null)
                _backgroundRenderer = GetComponent<SpriteRenderer>();

            SetObjectRendererTemplate(_objectRenderer);
        }
        
        public void SetObject(GameObject obj)
        {
            SetObject(obj, Color.white);
        }

        public void SetObject(GameObject obj, Color color)
        {
            SetObject(obj, color, BlockBlastSpriteLibrary.CatBlockSprite, obj != null ? obj.GetInstanceID() : -1);
        }

        public void SetObject(GameObject obj, Color color, int groupId)
        {
            SetObject(obj, color, BlockBlastSpriteLibrary.CatBlockSprite, groupId);
        }

        public void SetObject(GameObject obj, Color color, Sprite sprite, int groupId)
        {
            SetObject(obj, color, sprite, groupId, null);
        }

        public void SetObject(GameObject obj, Color color, Sprite sprite, int groupId, Vector3? moveFromWorldPosition)
        {
            SetObject(obj, color, sprite, groupId, moveFromWorldPosition, false);
        }

        public void SetStageCheeseObject(GameObject obj, Sprite sprite, int groupId)
        {
            SetObject(obj, Color.white, sprite, groupId, null, true);
        }

        private void SetObject(GameObject obj, Color color, Sprite sprite, int groupId, Vector3? moveFromWorldPosition, bool isStageCheese)
        {
            _isHaveObject = true;
            _thisObject = obj;
            _currentColor = color;
            _currentSprite = sprite != null ? sprite : BlockBlastSpriteLibrary.CatBlockSprite;
            _visualSprite = _currentSprite;
            _currentGroupId = groupId;
            _isStageCheese = isStageCheese;

            if (_objectRenderer == null)
                _objectRenderer = BlockCellVisualPool.Get(
                    _objectRendererTemplate,
                    transform,
                    _objectRendererHomeLocalPosition,
                    _objectRendererHomeLocalRotation,
                    _objectRendererHomeLocalScale);

            bool hasCatSprite = ApplyObjectSprite();
            _objectRenderer.color = hasCatSprite ? Color.white : color;
            _objectRenderer.enabled = true;

            MoveObjectRenderer(moveFromWorldPosition);
        }

        public void ClearObject()
        {
            _isHaveObject = false;
            _thisObject = null;
            _currentColor = Color.white;
            _currentSprite = null;
            _visualSprite = null;
            _currentGroupId = -1;
            _isStageCheese = false;

            ReleaseObjectRenderer();
        }

        public void ReassignGroup(int groupId)
        {
            if (_isHaveObject && !_isStageCheese)
                _currentGroupId = groupId;
        }

        public void SetVisualSprite(Sprite sprite)
        {
            if (!_isHaveObject || _isStageCheese)
                return;

            _visualSprite = sprite != null ? sprite : _currentSprite;
            bool hasCatSprite = ApplyObjectSprite();

            if (_objectRenderer != null)
                _objectRenderer.color = hasCatSprite ? Color.white : _currentColor;
        }

        public void Configure(Vector2Int point, SpriteRenderer backgroundRenderer, SpriteRenderer objectRenderer)
        {
            ReleaseObjectRenderer();

            Point = point;
            _backgroundRenderer = backgroundRenderer;
            SetObjectRendererTemplate(objectRenderer);

            ClearObject();
        }

        public void SetBackgroundColor(Color color)
        {
            if (_backgroundRenderer != null)
                _backgroundRenderer.color = color;
        }

        private bool ApplyObjectSprite()
        {
            Sprite sprite = _visualSprite != null ? _visualSprite : BlockBlastSpriteLibrary.CatBlockSprite;

            if (_objectRenderer == null || sprite == null)
                return false;

            _objectRenderer.sprite = sprite;
            return true;
        }

        private void MoveObjectRenderer(Vector3? moveFromWorldPosition)
        {
            if (_objectRenderer == null)
                return;

            StopObjectMove();

            Vector3 targetPosition = GetObjectRendererHomeWorldPosition();

            if (!moveFromWorldPosition.HasValue || _objectMoveDuration <= 0f)
            {
                SnapObjectRendererToHome();
                return;
            }

            Vector3 startPosition = moveFromWorldPosition.Value;
            startPosition.z = targetPosition.z;
            _objectRenderer.transform.position = startPosition;
            _objectMoveTween = _objectRenderer.transform
                .DOMove(targetPosition, _objectMoveDuration, false)
                .SetEase(_objectMoveEase)
                .OnComplete(SnapObjectRendererToHome);
        }

        private void StopObjectMove()
        {
            if (_objectMoveTween != null && _objectMoveTween.IsActive())
                _objectMoveTween.Kill();

            _objectMoveTween = null;

            if (_objectRenderer != null)
                _objectRenderer.transform.DOKill();
        }

        private void CaptureObjectRendererHomePosition()
        {
            if (_hasObjectRendererHomeLocalPosition)
                return;

            SpriteRenderer sourceRenderer = ObjectRendererTemplate;

            if (sourceRenderer == null)
            {
                _objectRendererHomeLocalPosition = Vector3.zero;
                _objectRendererHomeLocalRotation = Quaternion.identity;
                _objectRendererHomeLocalScale = Vector3.one;
                _hasObjectRendererHomeLocalPosition = true;
                return;
            }

            Transform sourceTransform = sourceRenderer.transform;
            _objectRendererHomeLocalPosition = sourceTransform.localPosition;
            _objectRendererHomeLocalRotation = sourceTransform.localRotation;
            _objectRendererHomeLocalScale = sourceTransform.localScale;
            _hasObjectRendererHomeLocalPosition = true;
        }

        private Vector3 GetObjectRendererHomeWorldPosition()
        {
            CaptureObjectRendererHomePosition();
            Transform parentTransform = _objectRenderer != null && _objectRenderer.transform.parent != null
                ? _objectRenderer.transform.parent
                : transform;

            return parentTransform.TransformPoint(_objectRendererHomeLocalPosition);
        }

        private void SnapObjectRendererToHome()
        {
            if (_objectRenderer == null)
                return;

            CaptureObjectRendererHomePosition();
            _objectRenderer.transform.localPosition = _objectRendererHomeLocalPosition;
            _objectRenderer.transform.localRotation = _objectRendererHomeLocalRotation;
            _objectRenderer.transform.localScale = _objectRendererHomeLocalScale;
        }

        private void SetObjectRendererTemplate(SpriteRenderer template)
        {
            _objectRendererTemplate = template;
            _objectRenderer = null;
            _hasObjectRendererHomeLocalPosition = false;
            CaptureObjectRendererHomePosition();

            if (_objectRendererTemplate != null)
                _objectRendererTemplate.enabled = false;
        }

        private void ReleaseObjectRenderer()
        {
            if (_objectRenderer == null)
                return;

            StopObjectMove();
            SnapObjectRendererToHome();
            BlockCellVisualPool.Release(_objectRenderer);
            _objectRenderer = null;
        }
    }
}
