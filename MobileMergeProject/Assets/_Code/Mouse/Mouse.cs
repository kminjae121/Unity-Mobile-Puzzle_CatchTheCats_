using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using _Code.Block;
using _Code.Field;
using DG.Tweening;

namespace _Code.Mouse
{
    public class Mouse : MonoBehaviour
    {
        private enum Corner
        {
            TopLeft,
            TopRight,
            BottomRight,
            BottomLeft
        }

        [SerializeField] private Corner _corner = Corner.TopLeft;
        [SerializeField, Min(0f), FormerlySerializedAs("_cornerPadding")] private float _cornerHorizontalPadding = 1.1f;
        [SerializeField, Min(0f)] private float _cornerVerticalPadding = 1f;
        [SerializeField] private float _positionXOffset;
        [SerializeField, FormerlySerializedAs("_cushionCenterYOffset")] private float _positionYOffset;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField, Min(0.01f)] private float _moveDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float _moveAnimationFrameDuration = 0.05f;
        [SerializeField, Min(0.01f)] private float _idleAnimationFrameDuration = 0.16f;

        private Coroutine _moveAnimationRoutine;
        private Coroutine _idleAnimationRoutine;

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            ApplySprite();
            PlayIdleAnimation();
        }

        private void OnDisable()
        {
            StopMoveAnimation();
            StopIdleAnimation();
            transform.DOKill();
        }

        public void Initialize(BlockField blockField)
        {
            ApplySprite();
            ApplyPosition(blockField, Vector2Int.zero, true);
        }

        public bool TryMove(Vector2Int direction, BlockField blockField)
        {
            Vector2Int moveDirection = NormalizeDirection(direction);

            if (!TryGetNextCorner(moveDirection, out Corner nextCorner))
                return false;

            _corner = nextCorner;
            ApplyPosition(blockField, moveDirection);
            return true;
        }

        public bool CanMove(Vector2Int direction)
        {
            return TryGetNextCorner(direction, out _);
        }

        public bool TryGetMoveGuideWorldPositions(
            Vector2Int direction,
            BlockField blockField,
            out Vector3 fromPosition,
            out Vector3 toPosition)
        {
            fromPosition = transform.position;
            toPosition = transform.position;

            if (blockField == null)
                return false;

            Vector2Int moveDirection = NormalizeDirection(direction);

            if (!TryGetNextCorner(moveDirection, out Corner nextCorner))
                return false;

            fromPosition = GetCornerWorldPosition(_corner, blockField);
            toPosition = GetCornerWorldPosition(nextCorner, blockField);
            return true;
        }

        private bool TryGetNextCorner(Vector2Int direction, out Corner nextCorner)
        {
            direction = NormalizeDirection(direction);
            nextCorner = _corner;

            switch (_corner)
            {
                case Corner.TopLeft:
                    if (direction == Vector2Int.right)
                    {
                        nextCorner = Corner.TopRight;
                        return true;
                    }

                    if (direction == Vector2Int.down)
                    {
                        nextCorner = Corner.BottomLeft;
                        return true;
                    }

                    return false;

                case Corner.TopRight:
                    if (direction == Vector2Int.left)
                    {
                        nextCorner = Corner.TopLeft;
                        return true;
                    }

                    if (direction == Vector2Int.down)
                    {
                        nextCorner = Corner.BottomRight;
                        return true;
                    }

                    return false;

                case Corner.BottomRight:
                    if (direction == Vector2Int.left)
                    {
                        nextCorner = Corner.BottomLeft;
                        return true;
                    }

                    if (direction == Vector2Int.up)
                    {
                        nextCorner = Corner.TopRight;
                        return true;
                    }

                    return false;

                case Corner.BottomLeft:
                    if (direction == Vector2Int.right)
                    {
                        nextCorner = Corner.BottomRight;
                        return true;
                    }

                    if (direction == Vector2Int.up)
                    {
                        nextCorner = Corner.TopLeft;
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        private void ApplyPosition(BlockField blockField, Vector2Int moveDirection, bool snap = false)
        {
            StopMoveAnimation();
            StopIdleAnimation();
            transform.DOKill();

            if (_renderer != null)
                _renderer.color = Color.white;

            if (blockField == null)
            {
                PlayIdleAnimation();
                return;
            }

            Vector3 position = GetCornerWorldPosition(_corner, blockField);

            if (snap)
            {
                transform.position = position;
                PlayIdleAnimation();
                return;
            }

            PlayMoveAnimation(moveDirection);
            transform.DOMove(position, _moveDuration, false)
                .SetEase(Ease.Linear)
                .OnComplete(PlayIdleAnimation);
        }

        private Vector3 GetCornerWorldPosition(Corner corner, BlockField blockField)
        {
            if (blockField == null)
                return transform.position;

            Vector3 bottomLeft = blockField.GetWorldPosition(Vector2Int.zero);
            Vector3 topRight = blockField.GetWorldPosition(new Vector2Int(blockField.Width - 1, blockField.Height - 1));
            float left = bottomLeft.x - _cornerHorizontalPadding + _positionXOffset;
            float right = topRight.x + _cornerHorizontalPadding + _positionXOffset;
            float bottom = bottomLeft.y - _cornerVerticalPadding + _positionYOffset;
            float top = topRight.y + _cornerVerticalPadding + _positionYOffset;
            Vector3 position = transform.position;

            switch (corner)
            {
                case Corner.TopLeft:
                    position.x = left;
                    position.y = top;
                    break;

                case Corner.TopRight:
                    position.x = right;
                    position.y = top;
                    break;

                case Corner.BottomRight:
                    position.x = right;
                    position.y = bottom;
                    break;

                case Corner.BottomLeft:
                    position.x = left;
                    position.y = bottom;
                    break;
            }

            return position;
        }

        private void ApplySprite()
        {
            if (_renderer == null || BlockBlastSpriteLibrary.MouseSprite == null)
                return;

            _renderer.sprite = BlockBlastSpriteLibrary.MouseSprite;
            _renderer.color = Color.white;
        }

        private void PlayMoveAnimation(Vector2Int direction)
        {
            if (_renderer == null)
                return;

            _moveAnimationRoutine = StartCoroutine(AnimateMoveSprites(direction));
        }

        private IEnumerator AnimateMoveSprites(Vector2Int direction)
        {
            Sprite[] sprites = BlockBlastSpriteLibrary.GetMouseMoveSprites(direction);

            if (sprites == null || sprites.Length == 0)
            {
                _moveAnimationRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            int frameIndex = 0;

            while (elapsed < _moveDuration)
            {
                _renderer.sprite = sprites[frameIndex % sprites.Length];
                frameIndex++;

                yield return new WaitForSeconds(_moveAnimationFrameDuration);
                elapsed += _moveAnimationFrameDuration;
            }

            _moveAnimationRoutine = null;
        }

        private void StopMoveAnimation()
        {
            if (_moveAnimationRoutine == null)
                return;

            StopCoroutine(_moveAnimationRoutine);
            _moveAnimationRoutine = null;
        }

        private void PlayIdleAnimation()
        {
            if (_renderer == null)
                return;

            if (_idleAnimationRoutine != null)
                return;

            _idleAnimationRoutine = StartCoroutine(AnimateIdleSprites());
        }

        private IEnumerator AnimateIdleSprites()
        {
            Sprite[] sprites = BlockBlastSpriteLibrary.MouseIdleSprites;

            if (sprites == null || sprites.Length == 0)
            {
                ApplySprite();
                _idleAnimationRoutine = null;
                yield break;
            }

            int frameIndex = 0;

            while (true)
            {
                _renderer.sprite = sprites[frameIndex % sprites.Length];
                frameIndex++;

                yield return new WaitForSeconds(_idleAnimationFrameDuration);
            }
        }

        private void StopIdleAnimation()
        {
            if (_idleAnimationRoutine == null)
                return;

            StopCoroutine(_idleAnimationRoutine);
            _idleAnimationRoutine = null;
        }

        private static Vector2Int NormalizeDirection(Vector2Int direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                return direction.x > 0 ? Vector2Int.right : Vector2Int.left;

            return direction.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }
}
