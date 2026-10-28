using System;

/// What happened. A member raises one of these, every listener receives it.
/// A struct, so each listener gets a copy and none can change what the others see.
[Serializable]
public struct SocialEvent
{
    public SocialEventKind kind;
    public PartyMember sender;
    public PartyMember target;
    public string text;
    public int value;

    /// Set by a stored delivery when it replays old events to a late joiner.
    /// Listeners that pay out (MentorBonus) must ignore replays or they pay twice.
    public bool isReplay;

    // sender and target are Unity objects, so these null checks also catch destroyed ones.
    public string SenderName => sender != null ? sender.displayName : "someone";
    public string TargetName => target != null ? target.displayName : "someone";

    public override string ToString()
    {
        string s;
        switch (kind)
        {
            case SocialEventKind.Joined:  s = $"{SenderName} joined"; break;
            case SocialEventKind.Left:    s = $"{SenderName} left"; break;
            case SocialEventKind.Downed:  s = $"{SenderName} is down!"; break;
            case SocialEventKind.Revived: s = $"{SenderName} revived {TargetName}"; break;
            case SocialEventKind.Message: s = $"{SenderName}: \"{text}\""; break;
            case SocialEventKind.LevelUp: s = $"{SenderName} reached level {value}"; break;
            case SocialEventKind.LogFound: s = $"Archive log recovered: {text}"; break;
            default:                      s = $"{SenderName}: {kind}"; break;
        }
        return isReplay ? s + "  (earlier)" : s;
    }
}
