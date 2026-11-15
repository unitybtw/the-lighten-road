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

        private readonly List<Vector3> beamPoints = new List<Vector3>();
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

            for (int bounce = 0; bounce < MaxBounces; bounce++)
            {
                Physics2D.SyncTransforms();
                var hits = Physics2D.RaycastAll(origin + dir * 0.04f, dir, MaxBeamDistance);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                RaycastHit2D? validHit = null;
                foreach (var h in hits)
                {
                    if (h.collider == null || h.collider.gameObject == gameObject) continue;
                    var m = h.collider.GetComponent<LightMirror2D>();
                    if (m != null)
                    {
                        if (m == lastMirror) continue;
                        validHit = h;
                        break;
                    }
                    var b = h.collider.GetComponent<DarknessBarrier2D>();
                    if (b != null && !b.IsMelted)
                    {
                        validHit = h;
                        break;
                    }
                    var v = h.collider.GetComponent<AdvancedEmberVein>();
                    if (v != null && v.IsCorrupted)
                    {
                        validHit = h;
                        break;
                    }
                    if (h.collider.GetComponent<CentralCoreFinale2D>() != null)
                    {
                        validHit = h;
                        break;
                    }
                    if (!h.collider.isTrigger && h.collider.GetComponent<Player.PlayerMotor2D>() == null)
                    {
                        validHit = h;
                        break;
                    }
                }

                if (!validHit.HasValue)
                {
                    beamPoints.Add(origin + dir * MaxBeamDistance);
                    break;
                }

                var hit = validHit.Value;
                beamPoints.Add(hit.point);

                var mirror = hit.collider.GetComponent<LightMirror2D>();
                if (mirror != null)
                {
                    if (mirror.IsCorrupted)
                    {
                        LastBeamAbsorbedByCorruptedMirror = true;
                        break;
                    }

                    Vector2 normal = mirror.GetNormal();
                    if (Mathf.Abs(Vector2.Dot(dir, normal)) < 0.05f)
                    {
                        break;
                    }
                    dir = Vector2.Reflect(dir, normal).normalized;
                    origin = hit.point;
                    lastMirror = mirror;
                    continue;
                }

                var barrier = hit.collider.GetComponent<DarknessBarrier2D>();
                if (barrier != null && !barrier.IsMelted)
                {
                    barrier.ApplyBeamHit(dt);
                    break;
                }

                var vein = hit.collider.GetComponent<AdvancedEmberVein>();
                if (vein != null && vein.IsCorrupted)
                {
                    vein.CleanseCorruption();
                    break;
                }

                var core = hit.collider.GetComponent<CentralCoreFinale2D>();
                if (core != null)
                {
                    hitCoreThisFrame = core;
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
            BeamLine.positionCount = beamPoints.Count;
            for (int i = 0; i < beamPoints.Count; i++)
            {
                BeamLine.SetPosition(i, beamPoints[i]);
            }
        }
    }
}
