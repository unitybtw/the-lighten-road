using UnityEngine;

/// STRATEGY 2 of 2 inside the Observer. Axis 3: Opt-in vs. Ambient.
/// Asked once, when a PartyMember starts: "do I hear this channel without asking?"
public abstract class MembershipPolicy : ScriptableObject
{
    public abstract bool JoinsOnSpawn(PartyMember member);
}
