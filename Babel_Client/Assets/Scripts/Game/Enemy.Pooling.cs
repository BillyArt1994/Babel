using System;
using Babel.Unity.Infrastructure.Pooling;
using UnityEngine;

namespace Babel
{
    public partial class Enemy : IPooledView
    {
        private Action<GameObject> _returnToPool;
        private bool _returnRequested;

        internal void BindPoolReturn(Action<GameObject> returnToPool)
        {
            _returnToPool = returnToPool ?? throw new ArgumentNullException(nameof(returnToPool));
            _returnRequested = false;
        }

        public void ResetForSpawn()
        {
            _returnRequested = false;
            _deathFeedbackTimer = 0f;
            _isDying = false;
            _deathCompleted = false;
            _speedBuffTimer = 0f;
            _speedBuffMult = 1f;
            HP = Mathf.Max(_maxHealth, 1f);

            RestoreHitFlashVisual();
            if (_animator == null) _animator = GetComponent<Animator>();
            _lastIsMoving = false;
            if (_animator != null) _animator.SetBool(AnimIsMoving, false);

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        public void ResetForDespawn()
        {
            _movement?.OnRemoved();
            _movement = null;
            _ability?.OnRemoved();
            _ability = null;

            RestoreHitFlashVisual();
            _speedBuffTimer = 0f;
            _speedBuffMult = 1f;
            _deathFeedbackTimer = 0f;
            _isDying = false;
            _deathCompleted = false;
            _lastIsMoving = false;
            if (_animator != null) _animator.SetBool(AnimIsMoving, false);

            currentPath = null;
            waveEventId = -1;
            _data = null;
            HP = 0f;
            _returnToPool = null;
            _returnRequested = true;
        }

        private void ReturnToPoolOrDestroy()
        {
            if (_returnRequested) return;
            _returnRequested = true;

            Action<GameObject> returnToPool = _returnToPool;
            if (returnToPool != null)
            {
                returnToPool(gameObject);
                return;
            }

            DestroyAfterDeath();
        }

        private void RestoreHitFlashVisual()
        {
            if (_hitFlashRenderer == null) ResolveHitFlashRenderer();
            if (_isHitFlashing && _hitFlashRenderer != null)
                _hitFlashRenderer.color = _hitFlashOriginalColor;
            _hitFlashTimer = 0f;
            _isHitFlashing = false;
        }
    }
}
