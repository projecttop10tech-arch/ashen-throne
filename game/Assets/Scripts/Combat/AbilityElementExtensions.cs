using AshenThrone.Data;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Extension methods mapping AbilityElement to DamageType and TileType.
    /// Centralizes all element→type conversions in one place for easy maintenance.
    /// </summary>
    public static class AbilityElementExtensions
    {
        /// <summary>Map card element to the corresponding DamageType used in hit resolution.</summary>
        public static DamageType ToDamageType(this AbilityElement element) => element switch
        {
            AbilityElement.Physical  => DamageType.Physical,
            AbilityElement.Fire      => DamageType.Fire,
            AbilityElement.Ice       => DamageType.Ice,
            AbilityElement.Lightning => DamageType.Lightning,
            AbilityElement.Shadow    => DamageType.Shadow,
            AbilityElement.Holy      => DamageType.Holy,
            AbilityElement.Arcane    => DamageType.Arcane,
            AbilityElement.Nature    => DamageType.Nature,
            _                        => DamageType.Physical
        };

        /// <summary>
        /// Map card element to a TileType for terrain-effect cards.
        /// Returns TileType.Normal if the element doesn't create a terrain tile.
        /// </summary>
        public static TileType ToTileType(this AbilityElement element) => element switch
        {
            AbilityElement.Fire      => TileType.Fire,
            AbilityElement.Ice       => TileType.Water,   // Ice creates water/slow tile
            AbilityElement.Shadow    => TileType.ShadowVeil,
            AbilityElement.Holy      => TileType.ArcaneLayLine,
            _                        => TileType.Normal   // Other elements don't create terrain
        };
    }
}
