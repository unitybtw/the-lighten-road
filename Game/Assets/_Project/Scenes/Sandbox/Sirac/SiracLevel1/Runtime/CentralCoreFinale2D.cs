using System.Collections.Generic;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Finale central crystal core requiring 4 converged light beams to trigger the game ending.
    /// </summary>
    public sealed class CentralCoreFinale2D : MonoBehaviour
    {
        [Min(1)] public int RequiredBeams = 4;
        public ChamberCompletionSequence CompletionSequence;
        public SpriteRenderer CoreRenderer;
        public Light2D CoreLight;

        private readonly HashSet<ProjectorVein2D> activeProjectors = new HashSet<ProjectorVein2D>();
        private int directBeamHits;

        public int ActiveBeamCount => activeProjectors.Count + directBeamHits;
        public bool IsGameEndingTriggered { get; private set; }

        public event System.Action OnFinaleCompleted;

        private void Awake()
        {
            if (CoreRenderer == null) CoreRenderer = GetComponent<SpriteRenderer>();
            if (CoreLight == null) CoreLight = GetComponentInChildren<Light2D>();
            RefreshVisual();
        }

        public void AddBeamSource(ProjectorVein2D projector)
        {
            if (projector == null || !activeProjectors.Add(projector)) return;
            EvaluateFinale();
        }

        public void RemoveBeamSource(ProjectorVein2D projector)
        {
            if (projector == null || !activeProjectors.Remove(projector)) return;
            RefreshVisual();
        }

        public void RegisterDirectBeam()
        {
            directBeamHits++;
            EvaluateFinale();
        }

        private void EvaluateFinale()
        {
            RefreshVisual();
            if (IsGameEndingTriggered || ActiveBeamCount < RequiredBeams) return;

            IsGameEndingTriggered = true;
            RefreshVisual();
            OnFinaleCompleted?.Invoke();

            if (CompletionSequence != null && !CompletionSequence.IsRunning)
            {
                var player = FindFirstObjectByType<PlayerRespawnController>();
                if (player != null)
                {
                    CompletionSequence.TryBegin(player);
                }
            }
        }

        private void RefreshVisual()
        {
            float ratio = RequiredBeams > 0 ? Mathf.Clamp01((float)ActiveBeamCount / RequiredBeams) : 0f;
            if (CoreRenderer != null)
            {
                CoreRenderer.color = IsGameEndingTriggered
                    ? new Color(1f, 0.96f, 0.80f, 1f)
                    : Color.Lerp(new Color(0.28f, 0.34f, 0.44f, 1f), new Color(1f, 0.80f, 0.36f, 1f), ratio);
            }
            if (CoreLight != null)
            {
                CoreLight.intensity = 0.25f + ratio * 1.35f;
                CoreLight.pointLightOuterRadius = 2.5f + ratio * 4.5f;
            }
        }
    }
}
