using System.Collections.Generic;
using _Code.Block;
using _Code.Field;
using _Code.SO;
using UnityEngine;
using MouseView = _Code.Mouse.Mouse;

namespace _Code.Manager
{
    public sealed class BoardShiftController : MonoBehaviour
    {
        [SerializeField] private BlockField blockField;
        [SerializeField] private RandomBlockManager randomBlockManager;
        [SerializeField] private MouseView mouse;
        [SerializeField, Min(20f)] private float swipeMinDistance = 75f;
        [SerializeField] private bool enableKeyboardInput = true;

        private readonly SwipeInputReader _swipeInputReader = new SwipeInputReader();

        public void Configure(
            BlockField blockField,
            RandomBlockManager randomBlockManager,
            MouseView mouse,
            float swipeMinDistance,
            bool enableKeyboardInput)
        {
            if (this.blockField == null)
                this.blockField = blockField;

            if (this.randomBlockManager == null)
                this.randomBlockManager = randomBlockManager;

            if (this.mouse == null)
                this.mouse = mouse;

            this.swipeMinDistance = Mathf.Max(20f, swipeMinDistance);
            this.enableKeyboardInput = enableKeyboardInput;
        }

        public bool TryReadDirection(bool isDraggingPiece, out Vector2Int direction)
        {
            direction = Vector2Int.zero;

            if (isDraggingPiece)
            {
                _swipeInputReader.Cancel();
                return false;
            }

            return _swipeInputReader.TryReadDirection(swipeMinDistance, enableKeyboardInput, out direction);
        }

        public void CancelInput()
        {
            _swipeInputReader.Cancel();
        }

        public bool TryShift(
            Vector2Int direction,
            ICollection<Vector3> clearedWorldPositions,
            ICollection<Vector2Int> clearedPoints,
            ICollection<Sprite> clearedSprites,
            bool groupByOriginalObject,
            out BoardShiftResult result)
        {
            result = default;
            clearedWorldPositions?.Clear();
            clearedPoints?.Clear();
            clearedSprites?.Clear();

            if (blockField == null || randomBlockManager == null)
                return false;
    
            if (mouse != null)
                mouse.TryMove(direction, blockField);

            bool moved = blockField.Compact(direction, groupByOriginalObject);
            int clearedLines = blockField.ClearCompletedLines(clearedWorldPositions, clearedPoints, clearedSprites);
            bool hasAnyRemainingPlacement = randomBlockManager.HasAnyAvailablePlacement(blockField);
            result = new BoardShiftResult(moved, clearedLines, hasAnyRemainingPlacement);
            return true;
        }

        public readonly struct BoardShiftResult
        {
            public BoardShiftResult(bool moved, int clearedLines, bool hasAnyRemainingPlacement)
            {
                Moved = moved;
                ClearedLines = clearedLines;
                HasAnyRemainingPlacement = hasAnyRemainingPlacement;
            }

            public bool Moved { get; }
            public int ClearedLines { get; }
            public bool HasAnyRemainingPlacement { get; }
            public bool HasVisibleChange => Moved || ClearedLines > 0;
        }

        private sealed class SwipeInputReader
        {
            private Vector2 _startPosition;
            private bool _isTracking;

            public bool TryReadDirection(float minDistance, bool enableKeyboardInput, out Vector2Int direction)
            {
                if (enableKeyboardInput && TryReadKeyboard(out direction))
                    return true;

                if (Input.touchCount > 0)
                    return TryReadTouch(minDistance, out direction);

                return TryReadMouse(minDistance, out direction);
            }

            public void Cancel()
            {
                _isTracking = false;
            }

            private bool TryReadTouch(float minDistance, out Vector2Int direction)
            {
                direction = Vector2Int.zero;
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    _startPosition = touch.position;
                    _isTracking = true;
                    return false;
                }

                if (!_isTracking)
                    return false;

                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                    return false;

                Vector2 delta = touch.position - _startPosition;
                _isTracking = false;
                return TryConvertDelta(delta, minDistance, out direction);
            }

            private bool TryReadMouse(float minDistance, out Vector2Int direction)
            {
                direction = Vector2Int.zero;

                if (Input.GetMouseButtonDown(0))
                {
                    _startPosition = Input.mousePosition;
                    _isTracking = true;
                    return false;
                }

                if (!_isTracking || !Input.GetMouseButtonUp(0))
                    return false;

                Vector2 delta = (Vector2)Input.mousePosition - _startPosition;
                _isTracking = false;
                return TryConvertDelta(delta, minDistance, out direction);
            }

            private static bool TryReadKeyboard(out Vector2Int direction)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    direction = Vector2Int.up;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                {
                    direction = Vector2Int.down;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                {
                    direction = Vector2Int.left;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    direction = Vector2Int.right;
                    return true;
                }

                direction = Vector2Int.zero;
                return false;
            }

            private static bool TryConvertDelta(Vector2 delta, float minDistance, out Vector2Int direction)
            {
                if (delta.sqrMagnitude < minDistance * minDistance)
                {
                    direction = Vector2Int.zero;
                    return false;
                }

                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    direction = delta.x > 0f ? Vector2Int.right : Vector2Int.left;
                else
                    direction = delta.y > 0f ? Vector2Int.up : Vector2Int.down;

                return true;
            }
        }
    }
}
