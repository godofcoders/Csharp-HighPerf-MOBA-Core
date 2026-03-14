using UnityEngine;
using UnityEngine.UI;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure.UI
{
    public class BrawlerUIBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BrawlerController _controller;
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Slider _ammoSlider;

        private void Start()
        {
            if (_controller == null || _controller.State == null) return;

            // 1. Initial Sync
            UpdateHealthUI(_controller.State.CurrentHealth);

            // 2. Subscribe to Events
            // This is the "Observer Pattern" - zero overhead when nothing is happening
            _controller.State.OnHealthChanged += UpdateHealthUI;
        }

        private void Update()
        {
            // Ammo is a "Continuous" value (it reloads smoothly), 
            // so we sync it in Update for visual smoothness.
            if (_controller.State != null)
            {
                _ammoSlider.value = _controller.State.Ammo.CurrentAmmo / _controller.State.Ammo.MaxAmmo;
            }
        }

        private void UpdateHealthUI(float currentHealth)
        {
            float ratio = currentHealth / _controller.State.MaxHealth.Value;
            _healthSlider.value = ratio;
        }

        private void OnDestroy()
        {
            // Always unsubscribe to prevent memory leaks!
            if (_controller != null && _controller.State != null)
            {
                _controller.State.OnHealthChanged -= UpdateHealthUI;
            }
        }
    }
}