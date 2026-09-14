using System.Collections.Generic;
using UnityEngine;

namespace _Code.Effects
{
    public sealed class LineClearPawParticleEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _pawParticles;
        [SerializeField] private ParticleSystem _sparkleParticles;
        [SerializeField] private bool _playOnStart;
        [SerializeField, Min(1)] private int _pawBurstCount = 18;
        [SerializeField, Min(0)] private int _sparkleBurstCount = 10;
        [SerializeField, Min(1)] private int _perBlockPawBurstCount = 4;
        [SerializeField, Min(0)] private int _perBlockSparkleBurstCount = 2;
        [SerializeField, Min(0.1f)] private float _releaseDelay = 1.25f;

        private ParticleSystem[] _particles;

        public float ReleaseDelay => _releaseDelay;
        public bool IsPlaying => HasAnyAliveParticle();

        private void Awake()
        {
            CacheParticles();
            StopAndClear();
        }

        private void Start()
        {
            if (_playOnStart)
                Play(transform.position, 1, Color.white);
        }

        public void Play(Vector3 worldPosition, int clearedLineCount, Color effectColor, float sizeMultiplier = 1f)
        {
            transform.position = worldPosition;
            gameObject.SetActive(true);
            StopAndClear();

            int burstMultiplier = Mathf.Max(1, clearedLineCount);
            Emit(_pawParticles, _pawBurstCount * burstMultiplier, effectColor, sizeMultiplier);
            Emit(_sparkleParticles, _sparkleBurstCount * burstMultiplier, effectColor, sizeMultiplier);
        }

        public void PlayAtPositions(
            IReadOnlyList<Vector3> worldPositions,
            int clearedLineCount,
            Color particleColor,
            float sizeMultiplier = 1f)
        {
            if (worldPositions == null || worldPositions.Count == 0)
            {
                Play(transform.position, clearedLineCount, particleColor, sizeMultiplier);
                return;
            }

            gameObject.SetActive(true);
            StopAndClear();

            foreach (Vector3 worldPosition in worldPositions)
            {
                EmitAt(_pawParticles, worldPosition, _perBlockPawBurstCount, particleColor, sizeMultiplier);
                EmitAt(_sparkleParticles, worldPosition, _perBlockSparkleBurstCount, particleColor, sizeMultiplier);
            }
        }

        public void StopAndClear()
        {
            CacheParticles();

            foreach (ParticleSystem particles in _particles)
                Clear(particles);
        }

        private static void Emit(ParticleSystem particles, int count, Color effectColor, float sizeMultiplier)
        {
            if (particles == null || count <= 0)
                return;

            Clear(particles);

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                startColor = effectColor
            };
            EmitWithSizeMultiplier(particles, emitParams, count, sizeMultiplier);
        }

        private static void EmitAt(
            ParticleSystem particles,
            Vector3 worldPosition,
            int count,
            Color particleColor,
            float sizeMultiplier)
        {
            if (particles == null || count <= 0)
                return;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = worldPosition,
                startColor = particleColor,
                applyShapeToPosition = true
            };
            EmitWithSizeMultiplier(particles, emitParams, count, sizeMultiplier);
        }

        private static void EmitWithSizeMultiplier(
            ParticleSystem particles,
            ParticleSystem.EmitParams emitParams,
            int count,
            float sizeMultiplier)
        {
            ParticleSystem.MainModule main = particles.main;
            float originalSizeMultiplier = main.startSizeMultiplier;
            main.startSizeMultiplier = originalSizeMultiplier * Mathf.Max(0.01f, sizeMultiplier);
            particles.Emit(emitParams, count);
            main.startSizeMultiplier = originalSizeMultiplier;
        }

        private static void Clear(ParticleSystem particles)
        {
            if (particles != null)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void CacheParticles()
        {
            if (_particles != null)
                return;

            if (_pawParticles == null || _sparkleParticles == null)
            {
                ParticleSystem[] childParticles = GetComponentsInChildren<ParticleSystem>(true);

                if (_pawParticles == null && childParticles.Length > 0)
                    _pawParticles = childParticles[0];

                if (_sparkleParticles == null && childParticles.Length > 1)
                    _sparkleParticles = childParticles[1];
            }

            List<ParticleSystem> particles = new List<ParticleSystem>(2);

            if (_pawParticles != null)
                particles.Add(_pawParticles);

            if (_sparkleParticles != null && _sparkleParticles != _pawParticles)
                particles.Add(_sparkleParticles);

            _particles = particles.ToArray();
        }

        private bool HasAnyAliveParticle()
        {
            CacheParticles();

            foreach (ParticleSystem particles in _particles)
            {
                if (particles != null && particles.IsAlive(true))
                    return true;
            }

            return false;
        }
    }
}
