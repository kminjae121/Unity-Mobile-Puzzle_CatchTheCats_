using UnityEngine;
using UnityEngine.UI;

namespace _Code.CatHome
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class CatResidentView : MonoBehaviour
    {
        [SerializeField] private HomeCatType catType;
        [SerializeField] private Image portrait;
        [SerializeField, Min(0f)] private float breathingAmount = 0.018f;
        [SerializeField, Min(0f)] private float swayDegrees = 1.5f;

        private float _displaySize = 0.62f;
        private float _targetSize = 0.62f;
        private float _reactionTime;
        private float _reactionDuration;
        private bool _grewUp;

        public HomeCatType CatType => catType;
        public Image Portrait => portrait;

        public void Configure(HomeCatType type, Image image)
        {
            catType = type;
            portrait = image;
            portrait.raycastTarget = false;
        }

        public void SetProgress(CatGrowthCatalog catalog, CatGrowthCatalog.CatDefinition cat, int lines, bool animate)
        {
            if (portrait == null)
                return;

            Sprite nextSprite = cat.GetSprite(catalog.GetStage(lines));
            _grewUp = portrait.sprite != nextSprite;
            portrait.sprite = nextSprite;
            _targetSize = catalog.GetSize(lines);
            portrait.enabled = nextSprite != null;
            if (!animate)
                _displaySize = _targetSize;
            else
            {
                _reactionTime = 0f;
                _reactionDuration = _grewUp ? 0.85f : 0.5f;
            }
            ApplyPose(0f);
        }

        private void Update()
        {
            _displaySize = Mathf.MoveTowards(_displaySize, _targetSize, Time.deltaTime * 0.5f);
            _reactionTime += Time.deltaTime;
            ApplyPose(Time.time);
        }

        private void ApplyPose(float time)
        {
            if (portrait == null)
                return;

            float phase = time * 2.1f + (int)catType * 1.7f;
            float breath = Mathf.Sin(phase) * breathingAmount;
            float reaction = _reactionTime < _reactionDuration
                ? Mathf.Sin(Mathf.PI * _reactionTime / _reactionDuration) : 0f;
            float bounce = reaction * (_grewUp ? 0.12f : 0.055f);
            RectTransform body = portrait.rectTransform;
            body.localScale = new Vector3(_displaySize * (1f - breath * 0.5f + reaction * 0.025f),
                _displaySize * (1f + breath + reaction * 0.04f), 1f);
            body.anchoredPosition = new Vector2(0f, ((RectTransform)transform).rect.height * bounce);
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.65f) * swayDegrees);
        }

        private void OnDisable()
        {
            _reactionDuration = 0f;
            if (portrait == null)
                return;
            portrait.rectTransform.anchoredPosition = Vector2.zero;
            portrait.rectTransform.localRotation = Quaternion.identity;
        }
    }
}
