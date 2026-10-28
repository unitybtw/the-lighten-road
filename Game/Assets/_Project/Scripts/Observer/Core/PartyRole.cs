using UnityEngine;

/// Axis 2: Symmetric vs. Asymmetric. What a member may do and may see.
/// One role asset shared by everyone = symmetric. Different assets per member = asymmetric.
/// The role decides. PartyMember asks it. The UI never checks a role at all.
[CreateAssetMenu(menuName = "Social/Role", fileName = "NewRole")]
public class PartyRole : ScriptableObject
{
    public string displayName = "Member";

    [Header("What this role may do")]
    public bool canRevive = true;
    public bool canLeaveMessages = true;

    [Header("What this role sees")]
    public bool seesStatusChanges = true;   // Downed, Revived
    public bool seesMessages = true;        // Message

    [Header("Flags other systems may read")]
    public bool isMentor = false;           // MentorBonus can pay mentors only

    public bool CanSee(SocialEvent e)
    {
        switch (e.kind)
        {
            case SocialEventKind.Downed:
            case SocialEventKind.Revived: return seesStatusChanges;
            case SocialEventKind.Message: return seesMessages;
            default:                      return true;
        }
    }
}
