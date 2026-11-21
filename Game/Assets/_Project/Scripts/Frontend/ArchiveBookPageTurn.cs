using System;
using TheLightenRoad.Audio;
using UnityEngine;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Frame-by-frame 2D pixel-art page turn driver using unscaled time while gameplay is paused.
    /// Commits the new spread at the midpoint frame as the turning leaf crosses the center gutter.
    /// </summary>
    public sealed class ArchiveBookPageTurn : MonoBehaviour
    {
        public const float TurnDuration = 0.42f;

        private ArchiveBookPresentation view;
        private Func<bool> canTurn;
        private Action<int> commitDirection;
        private Action pendingCustomCommit;

        private bool busy;
        private bool committed;
        private int direction = 1;
        private float elapsed;
        private float progress;

        public bool IsBusy => busy;
        public float Progress => progress;
        public int Direction => direction;
        public int CurrentFrameIndex => busy
            ? Mathf.Clamp(Mathf.FloorToInt(progress * PixelArchiveBook.FrameCount), 0, PixelArchiveBook.FrameCount - 1)
            : -1;

        public void Initialize(ArchiveBookPresentation presentation, Func<bool> canTurnCallback, Action<int> commitCallback)
        {
            view = presentation;
            canTurn = canTurnCallback;
            commitDirection = commitCallback;
        }

        public void Automatic(int turnDirection)
        {
            if (busy || canTurn == null || commitDirection == null || view == null || !canTurn()) return;
            int dir = turnDirection >= 0 ? 1 : -1;
            if (MenuPreferences.ReducedMotion)
            {
                commitDirection(dir);
                PlayTurnSound();
                return;
            }
            StartTurn(dir, customCommit: null);
        }

        public void AutomaticTo(int turnDirection, Action onMidpointCommit)
        {
            if (busy || view == null || onMidpointCommit == null) return;
            int dir = turnDirection >= 0 ? 1 : -1;
            if (MenuPreferences.ReducedMotion)
            {
                onMidpointCommit();
                PlayTurnSound();
                return;
            }
            StartTurn(dir, customCommit: onMidpointCommit);
        }

        private void StartTurn(int dir, Action customCommit)
        {
            direction = dir;
            pendingCustomCommit = customCommit;
            elapsed = 0f;
            progress = 0f;
            committed = false;
            busy = true;

            view.SetPageTurnFrame(0, direction, visible: true);
            view.SpawnPageTurnDust(direction);
            PlayTurnSound();
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        public void Tick(float dt)
        {
            if (!busy || view == null) return;

            elapsed += Mathf.Max(0f, dt);
            progress = Mathf.Clamp01(elapsed / TurnDuration);

            // Slight ease-in-out so the corner lift and final settle feel tactile in 2D pixel frames.
            float shaped = progress * progress * (3f - 2f * progress);
            int frame = Mathf.Clamp(Mathf.FloorToInt(shaped * PixelArchiveBook.FrameCount), 0, PixelArchiveBook.FrameCount - 1);

            if (!committed && progress >= 0.5f)
            {
                committed = true;
                if (pendingCustomCommit != null)
                {
                    var action = pendingCustomCommit;
                    pendingCustomCommit = null;
                    action();
                }
                else
                {
                    commitDirection?.Invoke(direction);
                }
            }

            if (progress >= 1f)
            {
                Finish();
            }
            else
            {
                view.SetPageTurnFrame(frame, direction, visible: true);
            }
        }

        private void Finish()
        {
            if (!committed)
            {
                committed = true;
                if (pendingCustomCommit != null)
                {
                    var action = pendingCustomCommit;
                    pendingCustomCommit = null;
                    action();
                }
                else
                {
                    commitDirection?.Invoke(direction);
                }
            }

            busy = false;
            progress = 0f;
            elapsed = 0f;
            pendingCustomCommit = null;
            view?.SetPageTurnFrame(0, direction, visible: false);
        }

        public void Cancel()
        {
            busy = false;
            committed = false;
            progress = 0f;
            elapsed = 0f;
            pendingCustomCommit = null;
            view?.SetPageTurnFrame(0, direction, visible: false);
        }

        private static void PlayTurnSound()
        {
            if (Application.isPlaying && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayUIHover(0.75f);
            }
        }

        private void OnDisable() => Cancel();
    }
}
