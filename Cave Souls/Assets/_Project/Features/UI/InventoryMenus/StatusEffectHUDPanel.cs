using CaveSouls.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Displays Bleed, Poison, Frostbite, and Curse buildup meters when accumulating on the player.
    /// Zero GC allocations.
    /// </summary>
    public sealed class StatusEffectHUDPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _playerObject;
        [SerializeField] private Image _bleedFillBar;
        [SerializeField] private Image _poisonFillBar;
        [SerializeField] private Image _frostbiteFillBar;
        [SerializeField] private Image _curseFillBar;

        private void OnEnable()
        {
            EventBus<OnStatusEffectBuildupEvent>.Subscribe(HandleStatusBuildup);
        }

        private void OnDisable()
        {
            EventBus<OnStatusEffectBuildupEvent>.Unsubscribe(HandleStatusBuildup);
        }

        private void HandleStatusBuildup(in OnStatusEffectBuildupEvent evt)
        {
            if (_playerObject != null && evt.Target != _playerObject) return;
            float ratio = evt.MaxThreshold > 0f ? Mathf.Clamp01(evt.CurrentBuildUp / evt.MaxThreshold) : 0f;

            switch (evt.EffectType)
            {
                case StatusEffectType.Bleed:
                    if (_bleedFillBar != null) _bleedFillBar.fillAmount = ratio;
                    break;
                case StatusEffectType.Poison:
                    if (_poisonFillBar != null) _poisonFillBar.fillAmount = ratio;
                    break;
                case StatusEffectType.Frostbite:
                    if (_frostbiteFillBar != null) _frostbiteFillBar.fillAmount = ratio;
                    break;
                case StatusEffectType.Curse:
                    if (_curseFillBar != null) _curseFillBar.fillAmount = ratio;
                    break;
            }
        }
    }
}
