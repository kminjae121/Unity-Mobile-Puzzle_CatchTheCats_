using System.Collections;
using System.Collections.Generic;
using _Code.Effects;
using Code.Core.Events.Bus;
using UnityEngine;

namespace _Code.Manager
{
    public sealed class LineClearEffectPlayer : MonoBehaviour
    {
        [SerializeField] private HapticFeedback hapticFeedback;
        [SerializeField] private LineClearPawParticleEffect lineClearParticleEffect;
        [SerializeField, Min(1)] private int _initialPoolSize = 4;
        [SerializeField, Min(0.1f)] private float _fallbackReleaseDelay = 1.5f;
        [SerializeField] private Transform _poolRoot;

        private readonly Queue<LineClearPawParticleEffect> _pool = new Queue<LineClearPawParticleEffect>();
        private readonly HashSet<LineClearPawParticleEffect> _pooledEffects = new HashSet<LineClearPawParticleEffect>();
        private bool _isPoolReady;

        private void Awake()
        {
            WarmPool();
        }

        public void Configure(HapticFeedback hapticFeedback, LineClearPawParticleEffect lineClearParticleEffect)
        {
            if (this.hapticFeedback == null)
                this.hapticFeedback = hapticFeedback;

            if (this.lineClearParticleEffect == null)
                this.lineClearParticleEffect = lineClearParticleEffect;

            WarmPool();
        }

        public void Play(
            int clearedLines,
            IReadOnlyList<Vector3> clearedBlockPositions,
            Color effectColor,
            float sizeMultiplier = 1f)
        {
            if (clearedLines <= 0)
                return;

            LineClearPawParticleEffect effect = GetEffect();
            if (effect != null)
            {
                effect.PlayAtPositions(clearedBlockPositions, clearedLines, effectColor, sizeMultiplier);
                StartCoroutine(ReturnAfterPlay(effect));
            }

            if (hapticFeedback != null)
                hapticFeedback.PlayLineClear();
        }

        private void WarmPool()
        {
            if (_isPoolReady || lineClearParticleEffect == null || !CanUseAsPrefab(lineClearParticleEffect))
                return;

            _isPoolReady = true;

            for (int i = 0; i < _initialPoolSize; i++)
                ReturnToPool(CreateEffect());
        }

        private LineClearPawParticleEffect GetEffect()
        {
            WarmPool();

            if (lineClearParticleEffect == null || !CanUseAsPrefab(lineClearParticleEffect))
                return null;

            while (_pool.Count > 0)
            {
                LineClearPawParticleEffect effect = _pool.Dequeue();

                if (effect != null)
                    return effect;
            }

            return CreateEffect();
        }

        private LineClearPawParticleEffect CreateEffect()
        {
            LineClearPawParticleEffect effect = Instantiate(lineClearParticleEffect, GetPoolRoot());
            effect.gameObject.name = lineClearParticleEffect.gameObject.name;
            effect.gameObject.SetActive(false);
            _pooledEffects.Add(effect);
            return effect;
        }

        private IEnumerator ReturnAfterPlay(LineClearPawParticleEffect effect)
        {
            float elapsed = 0f;
            float maxWait = Mathf.Max(_fallbackReleaseDelay, effect.ReleaseDelay);

            while (effect != null && effect.IsPlaying && elapsed < maxWait)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (effect != null)
                ReturnToPool(effect);
        }

        private void ReturnToPool(LineClearPawParticleEffect effect)
        {
            if (effect == null)
                return;

            effect.StopAndClear();
            effect.transform.SetParent(GetPoolRoot(), false);
            effect.gameObject.SetActive(false);

            if (_pooledEffects.Contains(effect))
                _pool.Enqueue(effect);
        }

        private Transform GetPoolRoot()
        {
            return _poolRoot != null ? _poolRoot : transform;
        }

        private static bool CanUseAsPrefab(LineClearPawParticleEffect effect)
        {
            return effect != null && !effect.gameObject.scene.IsValid();
        }
    }
}
