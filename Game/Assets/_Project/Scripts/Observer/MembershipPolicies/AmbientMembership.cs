using UnityEngine;

/// AMBIENT. Everyone who spawns is already listening. Journey's partner,
/// Dark Souls messages, Helldivers' galactic war.
[CreateAssetMenu(menuName = "Social/Membership/Ambient", fileName = "AmbientMembership")]
public class AmbientMembership : MembershipPolicy
{
    public override bool JoinsOnSpawn(PartyMember member) => true;
}
