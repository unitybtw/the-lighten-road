/// What can happen on a channel. Add a kind here and no existing listener breaks:
/// each listener ignores the kinds it does not handle.
public enum SocialEventKind
{
    Joined,
    Left,
    Downed,
    Revived,
    Message,
    LevelUp,
    LogFound
}
