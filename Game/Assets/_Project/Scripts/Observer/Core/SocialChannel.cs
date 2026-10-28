using System.Collections.Generic;
using UnityEngine;

/// The Subject. An asset, so sources and listeners both reference it and never each other.
/// Notice what it does NOT know about:
///   - who is listening (a List<ISocialListener>, nothing more specific)
///   - whether late joiners hear history (DeliveryPolicy decides)
///   - whether a member joins automatically (MembershipPolicy decides, PartyMember asks)
[CreateAssetMenu(menuName = "Social/Channel", fileName = "NewChannel")]
public class SocialChannel : ScriptableObject
{
    [Header("Axis 1: Synchronous vs. Asynchronous")]
    public DeliveryPolicy delivery;

    [Header("Axis 3: Opt-in vs. Ambient")]
    public MembershipPolicy membership;

    private readonly List<ISocialListener> listeners = new List<ISocialListener>();
    public int ListenerCount => listeners.Count;

    // Runtime state in a ScriptableObject survives between Play sessions in the Editor.
    // Clear it every time the asset is (re)loaded.
    void OnEnable() => listeners.Clear();

    public void Subscribe(ISocialListener listener)
    {
        if (listener == null || listeners.Contains(listener)) return;
        listeners.Add(listener);
        // A stored channel replays its log here.
        if (delivery != null) delivery.OnSubscribed(this, listener);
    }

    public void Unsubscribe(ISocialListener listener) => listeners.Remove(listener);

    public void Raise(SocialEvent e)
    {
        if (delivery == null)
        {
            Debug.LogWarning($"SocialChannel '{name}' has no DeliveryPolicy. {e.kind} dropped.");
            return;
        }
        delivery.Deliver(this, e, listeners);
    }
}
