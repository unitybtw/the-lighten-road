using System.Collections.Generic;
using UnityEngine;

/// ASYNCHRONOUS. The channel remembers the last N events and replays them to
/// whoever subscribes later. Dark Souls messages, Death Stranding structures.
/// "Variation 3" from the notes: variation 2 plus a log.
[CreateAssetMenu(menuName = "Social/Delivery/Stored", fileName = "StoredDelivery")]
public class StoredDelivery : DeliveryPolicy
{
    [Min(1)] public int capacity = 50;

    // The log lives on the asset. Two channels that share one StoredDelivery asset
    // share one log, so make one asset per channel.
    private readonly List<SocialEvent> log = new List<SocialEvent>();
    public int Count => log.Count;

    void OnEnable() => log.Clear();   // SO state survives Play sessions in the Editor

    public override void Deliver(SocialChannel channel, SocialEvent e,
                                 List<ISocialListener> listeners)
    {
        log.Add(e);
        if (log.Count > capacity) log.RemoveAt(0);
        foreach (var l in listeners.ToArray()) l.OnSocialEvent(e);
    }

    public override void OnSubscribed(SocialChannel channel, ISocialListener listener)
    {
        // The late joiner sees the past. Marked as a replay so nobody pays out twice.
        foreach (var stored in log.ToArray())
        {
            var e = stored;          // struct copy; the log keeps the original
            e.isReplay = true;
            listener.OnSocialEvent(e);
        }
    }

    public void Clear() => log.Clear();
}
