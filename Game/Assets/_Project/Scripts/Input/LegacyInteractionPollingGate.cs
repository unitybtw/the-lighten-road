using System.Collections.Generic;
using UnityEngine;

namespace TheLightenRoad.Input
{
    /// <summary>Shared leases for legacy raw-button polling. Never changes circuit/device state.</summary>
    public static class LegacyInteractionPollingGate
    {
        private sealed class Lease
        {
            public MonoBehaviour Actor;
            public bool WasEnabled;
            public readonly HashSet<int> Owners = new HashSet<int>();
        }
        private static readonly Dictionary<int, Lease> leases = new Dictionary<int, Lease>();
        private static readonly List<int> released = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { leases.Clear(); released.Clear(); }

        public static void Acquire(Object owner)
        {
            if (owner == null) return;
            int id = owner.GetInstanceID();
            foreach (var actor in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (actor.GetType().FullName != "TheLightenRoad.Interaction.EmberVein") continue;
                int key = actor.GetInstanceID();
                if (!leases.TryGetValue(key, out var lease))
                {
                    lease = new Lease { Actor = actor, WasEnabled = actor.enabled };
                    leases.Add(key, lease);
                }
                lease.Owners.Add(id);
                actor.enabled = false;
            }
        }

        public static void Release(Object owner)
        {
            if (owner == null) return;
            released.Clear();
            foreach (var pair in leases)
            {
                pair.Value.Owners.Remove(owner.GetInstanceID());
                if (pair.Value.Owners.Count != 0 && pair.Value.Actor != null) continue;
                if (pair.Value.Actor != null) pair.Value.Actor.enabled = pair.Value.WasEnabled;
                released.Add(pair.Key);
            }
            foreach (int key in released) leases.Remove(key);
            released.Clear();
        }
    }
}
