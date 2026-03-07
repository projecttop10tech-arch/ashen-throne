using UnityEngine;

namespace AshenThrone.Heroes
{
    /// <summary>
    /// ScriptableObject defining XP curves and level caps.
    /// All tunable progression values live here — never hardcoded in logic.
    /// Matches the balance curve in tools/BalanceSheets/ProgressionCurve.xlsx.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionConfig", menuName = "AshenThrone/Progression Config", order = 10)]
    public class ProgressionConfig : ScriptableObject
    {
        [Header("Hero Levels")]
        /// <summary>Maximum hero level. Tied to Stronghold level gate: max level = Stronghold level * 4.</summary>
        public int MaxHeroLevel = 80;

        [Header("XP Curve")]
        /// <summary>
        /// XP required to advance from level N to N+1.
        /// Array index = level (0-indexed, so index 0 = XP to go from level 1 to 2).
        /// Length must be MaxHeroLevel - 1.
        /// Formula: xp[n] = BaseXp * (1 + GrowthFactor)^n
        /// </summary>
        public int[] XpPerLevel;

        [Header("XP Curve Parameters (for editor regeneration)")]
        public int BaseXp = 100;
        [Range(1.05f, 1.3f)] public float GrowthFactor = 1.12f;

        [Header("Combat XP Rewards")]
        public int XpPerNormalBattleWin = 50;
        public int XpPerEliteBattleWin = 150;
        public int XpPerBossBattleWin = 400;
        public int XpPerPvpWin = 100;
        public int XpPerPvpLoss = 25;

        [Header("Stat Scaling")]
        /// <summary>Multiplicative bonus applied per level: finalStat = baseStat * (1 + StatGrowthPerLevel * (level-1))</summary>
        [Range(0.04f, 0.15f)] public float StatGrowthPerLevel = 0.08f;

        /// <summary>Returns XP required to advance from level to level+1. Returns int.MaxValue at max level.</summary>
        public int GetXpForLevel(int level)
        {
            if (level >= MaxHeroLevel) return int.MaxValue;
            int index = level - 1; // level 1 = index 0
            if (XpPerLevel == null || index >= XpPerLevel.Length)
            {
                // Fallback formula if array not populated
                return Mathf.RoundToInt(BaseXp * Mathf.Pow(GrowthFactor, index));
            }
            return XpPerLevel[index];
        }

#if UNITY_EDITOR
        /// <summary>Regenerates XpPerLevel array from BaseXp and GrowthFactor. Call from editor button.</summary>
        [ContextMenu("Regenerate XP Curve")]
        private void RegenerateXpCurve()
        {
            XpPerLevel = new int[MaxHeroLevel - 1];
            for (int i = 0; i < XpPerLevel.Length; i++)
                XpPerLevel[i] = Mathf.RoundToInt(BaseXp * Mathf.Pow(GrowthFactor, i));
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
