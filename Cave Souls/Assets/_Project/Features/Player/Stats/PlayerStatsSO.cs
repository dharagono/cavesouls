using UnityEngine;

namespace CaveSouls.Features.Player
{
    /// <summary>
    /// Immutable ScriptableObject holding base RPG level curves for Vigor, Mind, Endurance,
    /// Strength, Dexterity, Intelligence, and Faith.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatsConfig", menuName = "CaveSouls/Player/Player Stats Curves SO")]
    public sealed class PlayerStatsSO : ScriptableObject
    {
        [Header("Starting Attributes (Level 1..99)")]
        [SerializeField] private int _startingVigor = 12;
        [SerializeField] private int _startingMind = 10;
        [SerializeField] private int _startingEndurance = 11;
        [SerializeField] private int _startingStrength = 13;
        [SerializeField] private int _startingDexterity = 12;
        [SerializeField] private int _startingIntelligence = 9;
        [SerializeField] private int _startingFaith = 9;

        [Header("Stat Scaling Curves (Stat 1..99)")]
        [SerializeField] private AnimationCurve _vigorToHealthCurve = AnimationCurve.Linear(1f, 300f, 99f, 2100f);
        [SerializeField] private AnimationCurve _mindToFocusCurve = AnimationCurve.Linear(1f, 50f, 99f, 450f);
        [SerializeField] private AnimationCurve _enduranceToStaminaCurve = AnimationCurve.Linear(1f, 80f, 99f, 200f);
        [SerializeField] private AnimationCurve _enduranceToEquipLoadCurve = AnimationCurve.Linear(1f, 45f, 99f, 160f);

        public int StartingVigor => _startingVigor;
        public int StartingMind => _startingMind;
        public int StartingEndurance => _startingEndurance;
        public int StartingStrength => _startingStrength;
        public int StartingDexterity => _startingDexterity;
        public int StartingIntelligence => _startingIntelligence;
        public int StartingFaith => _startingFaith;

        public float EvaluateMaxHealth(int vigor) => _vigorToHealthCurve.Evaluate(Mathf.Clamp(vigor, 1, 99));
        public float EvaluateMaxFocus(int mind) => _mindToFocusCurve.Evaluate(Mathf.Clamp(mind, 1, 99));
        public float EvaluateMaxStamina(int endurance) => _enduranceToStaminaCurve.Evaluate(Mathf.Clamp(endurance, 1, 99));
        public float EvaluateMaxEquipLoad(int endurance) => _enduranceToEquipLoadCurve.Evaluate(Mathf.Clamp(endurance, 1, 99));
    }
}
