namespace GameData.Core.Models.Activities
{
    /// <summary>
    /// Group-role template for an activity. The catalog test asserts
    /// <c>Tanks + Healers + Dps + Support</c> is between MinPlayers and MaxPlayers.
    /// </summary>
    public sealed record RoleTemplate(int Tanks, int Healers, int Dps, int Support = 0);
}
