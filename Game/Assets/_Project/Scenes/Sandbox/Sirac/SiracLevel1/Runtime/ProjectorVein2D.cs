using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Projector Vein casting a reflected light ray through LightMirror2D targets,
    /// melting DarknessBarrier2D obstacles, cleansing corrupted AdvancedEmberVeins,
    /// and energizing CentralCoreFinale2D.
    /// </summary>
    public sealed class ProjectorVein2D : MonoBehaviour
    {
        public AdvancedEmberVein SourceVein;
        public Vector2 EmitDirection = Vector2.right;
        [Min(1f)] public float MaxBeamDistance = 28f;
        [Range(1, 8)] public int MaxBounces = 6;
        public LineRenderer BeamLine;

        private readonly List<Vector3> beamPoints = new List<Vector3>(8);
        private readonly List<Vector3> lastSyncedPoints = new List<Vector3>(8);
        private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[24];
        private static readonly ContactFilter2D RaycastFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false
        };
        private CentralCoreFinale2D activeHitCore;

        public IReadOnlyList<Vector3> LastBeamPoints => beamPoints;
        public bool IsEmitting => SourceVein == null || (SourceVein.VeinStateValue != 0 && !SourceVein.IsCorrupted);
        public bool LastBeamAbsorbedByCorruptedMirror { get; private set; }

        private void Awake()
        {
            if (BeamLine == null) BeamLine = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
        }

        private void OnDisable()
        {
            if (activeHitCore != null)
            {
                activeHitCore.RemoveBeamSource(this);
                activeHitCore = null;
            }
        }

        private void ConfigureLineRenderer()
        {
            if (BeamLine == null) return;
            if (BeamLine.sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    BeamLine.sharedMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
            }
            BeamLine.useWorldSpace = true;
            BeamLine.startWidth = 0.14f;
            BeamLine.endWidth = 0.09f;
            BeamLine.numCapVertices = 4;
            BeamLine.numCornerVertices = 4;
            BeamLine.startColor = new Color(1f, 0.92f, 0.62f, 0.95f);
            BeamLine.endColor = new Color(1f, 0.78f, 0.34f, 0.80f);
            BeamLine.sortingOrder = 8;
        }

        private void Update()
        {
            EvaluateBeam(Time.deltaTime);
            if (BeamLine != null && BeamLine.positionCount >= 2)
            {
                BeamLine.widthMultiplier = 1f + Mathf.Sin(Time.time * 7.5f) * 0.12f;
            }
        }

        public void EvaluateBeam(float dt)
        {
            beamPoints.Clear();
            LastBeamAbsorbedByCorruptedMirror = false;
            CentralCoreFinale2D hitCoreThisFrame = null;

            if (!IsEmitting)
            {
                if (activeHitCore != null)
                {
                    activeHitCore.RemoveBeamSource(this);
                    activeHitCore = null;
                }
                SyncLineRenderer();
                return;
            }

            Vector2 origin = transform.position;
            Vector2 dir = EmitDirection.sqrMagnitude > 0.0001f ? EmitDirection.normalized : Vector2.right;
            beamPoints.Add(origin);

            LightMirror2D lastMirror = null;
            Physics2D.SyncTransforms();

            for (int bounce = 0; bounce < MaxBounces; bounce++)
            {
                int hitCount = Physics2D.Raycast(origin + dir * 0.04f, dir, RaycastFilter, hitBuffer, MaxBeamDistance);
                for (int i = 1; i < hitCount; i++)
                {
                    var key = hitBuffer[i];
                    int j = i - 1;
                    while (j >= 0 && hitBuffer[j].distance > key.distance)
                    {
                        hitBuffer[j + 1] = hitBuffer[j];
                        j--;
                    }
                    hitBuffer[j + 1] = key;
                }

                bool hasValidHit = false;
                RaycastHit2D hit = default;
                LightMirror2D hitMirror = null;
                DarknessBarrier2D hitBarrier = null;
                AdvancedEmberVein hitVein = null;
                CentralCoreFinale2D hitCore = null;

                for (int i = 0; i < hitCount; i++)
                {
                    var h = hitBuffer[i];
                    if (h.collider == null || h.collider.gameObject == gameObject) continue;
                    if (h.collider.TryGetComponent<LightMirror2D>(out var m))
                    {
                        if (m == lastMirror) continue;
                        hasValidHit = true;
                        hit = h;
                        hitMirror = m;
                        break;
                    }
                    if (h.collider.TryGetComponent<DarknessBarrier2D>(out var b) && !b.IsMelted)
                    {
                        hasValidHit = true;
                        hit = h;
                        hitBarrier = b;
                        break;
                    }
                    if (h.collider.TryGetComponent<AdvancedEmberVein>(out var v) && v.IsCorrupted)
                    {
                        hasValidHit = true;
                        hit = h;
                        hitVein = v;
                        break;
                    }
                    if (h.collider.TryGetComponent<CentralCoreFinale2D>(out var c))
                    {
                        hasValidHit = true;
                        hit = h;
                        hitCore = c;
                        break;
                    }
                    if (!h.collider.isTrigger && !h.collider.TryGetComponent<Player.PlayerMotor2D>(out _))
                    {
                        hasValidHit = true;
                        hit = h;
                        break;
                    }
                }

                if (!hasValidHit)
                {
                    beamPoints.Add(origin + dir * MaxBeamDistance);
                    break;
                }

                beamPoints.Add(hit.point);

                if (hitMirror != null)
                {
                    if (hitMirror.IsCorrupted)
                    {
                        LastBeamAbsorbedByCorruptedMirror = true;
                        break;
                    }

                    Vector2 normal = hitMirror.GetNormal();
                    if (Mathf.Abs(Vector2.Dot(dir, normal)) < 0.05f)
                    {
                        break;
                    }
                    dir = Vector2.Reflect(dir, normal).normalized;
                    origin = hit.point;
                    lastMirror = hitMirror;
                    continue;
                }

                if (hitBarrier != null && !hitBarrier.IsMelted)
                {
                    hitBarrier.ApplyBeamHit(dt);
                    break;
                }

                if (hitVein != null && hitVein.IsCorrupted)
                {
                    hitVein.CleanseCorruption();
                    break;
                }

                if (hitCore != null)
                {
                    hitCoreThisFrame = hitCore;
                    break;
                }

                // Stopped on solid geometry
                if (!hit.collider.isTrigger)
                {
                    break;
                }
            }

            if (activeHitCore != hitCoreThisFrame)
            {
                if (activeHitCore != null) activeHitCore.RemoveBeamSource(this);
                activeHitCore = hitCoreThisFrame;
            }
            if (activeHitCore != null)
            {
                activeHitCore.AddBeamSource(this);
            }

            SyncLineRenderer();
        }

        private void SyncLineRenderer()
        {
            if (BeamLine == null) return;

            int count = beamPoints.Count;
            if (count == lastSyncedPoints.Count)
            {
                bool unchanged = true;
                for (int i = 0; i < count; i++)
                {
                    if ((beamPoints[i] - lastSyncedPoints[i]).sqrMagnitude > 0.00001f)
                    {
                        unchanged = false;
                        break;
                    }
                }
                if (unchanged) return;
            }

            BeamLine.positionCount = count;
            lastSyncedPoints.Clear();
            for (int i = 0; i < count; i++)
            {
                Vector3 pt = beamPoints[i];
                BeamLine.SetPosition(i, pt);
                lastSyncedPoints.Add(pt);
            }
        }
    }
}
