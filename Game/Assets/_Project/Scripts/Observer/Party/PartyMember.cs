using System;
using UnityEngine;

/// A player in the scene. It raises events and never calls a UI, a bonus or a feed.
/// It is also a listener: whatever it hears, and its role lets it see, it re-raises as a
/// plain C# event (variation 1) for anything attached to this one player.
public class PartyMember : MonoBehaviour, ISocialListener
{
    public string displayName = "Player";

    [Header("Wiring")]
    public SocialChannel channel;
    public PlayerContext player;            // from week 5. Bonuses write here.
    [Tooltip("An opt-in channel lets the host in on spawn. Everyone else presses J.")]
    public bool isHost = false;

    [Header("Axis 2: Symmetric vs. Asymmetric")]
    public PartyRole role;

    public bool IsDown { get; private set; }
    public bool IsMember { get; private set; }

    /// Variation 1, per member. Fires only for events this member's role can see.
    public event Action<SocialEvent> Heard;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerContext>();
    }

    // Start, not OnEnable. Things attached to this member subscribe to Heard in their
    // OnEnable, and every OnEnable runs before any Start, so a stored channel's replay
    // reaches them.
    void Start()
    {
        if (channel != null && channel.membership != null && channel.membership.JoinsOnSpawn(this))
            Join();                                           // ambient path
    }

    void OnDisable() => Leave();

    // ---- Things a member can do. Each one is a Raise and nothing else. ----

    public void Join()                                        // opt-in path
    {
        if (IsMember || channel == null) return;
        IsMember = true;
        channel.Subscribe(this);
        Raise(SocialEventKind.Joined);
    }

    public void Leave()
    {
        if (!IsMember || channel == null) return;
        Raise(SocialEventKind.Left);
        channel.Unsubscribe(this);
        IsMember = false;
    }

    public void GoDown()
    {
        if (IsDown) return;
        IsDown = true;
        Raise(SocialEventKind.Downed);
    }

    public bool Revive(PartyMember other)
    {
        if (other == null || other == this || !other.IsDown || IsDown) return false;
        if (role != null && !role.canRevive) return false;    // Axis 2 decides
        other.IsDown = false;
        Raise(SocialEventKind.Revived, target: other);
        return true;
    }

    public void LeaveMessage(string text)
    {
        if (role != null && !role.canLeaveMessages) return;   // Axis 2 decides
        Raise(SocialEventKind.Message, text: text);
    }

    /// For systems that live on this member and have something to announce
    /// (see LevelUpBroadcaster). Same rules as every other Raise.
    public void Broadcast(SocialEventKind kind, int value = 0, string text = null)
        => Raise(kind, text: text, value: value);

    // ---- The listener side. ----

    public void OnSocialEvent(SocialEvent e)
    {
        if (role != null && !role.CanSee(e)) return;          // Axis 2 decides, again
        Heard?.Invoke(e);
    }

    // Membership works in both directions: a member who has not joined neither hears
    // the channel nor is heard on it.
    private void Raise(SocialEventKind kind, PartyMember target = null,
                       string text = null, int value = 0)
    {
        if (channel == null || !IsMember) return;
        channel.Raise(new SocialEvent
        {
            kind = kind, sender = this, target = target, text = text, value = value
        });
    }
}
