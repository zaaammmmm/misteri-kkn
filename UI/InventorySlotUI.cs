using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace KKN.Game.UI
{
    /// <summary>
    /// Komponen yang di-attach pada prefab slot inventory.
    ///
    /// Hierarchy prefab:
    ///   InventorySlot (Button, Image background)
    ///     ├─ ItemIcon   (Image)
    ///     ├─ ItemLabel  (TMP_Text)
    ///     └─ BadgeText  (TMP_Text — untuk "×3" dll)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] private Image    itemIcon;
        [SerializeField] private TMP_Text itemLabel;
        [SerializeField] private TMP_Text badgeText;

        [Header("Highlight")]
        [SerializeField] private Image   background;
        [SerializeField] private Color   normalColor   = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        [SerializeField] private Color   selectedColor = new Color(0.3f, 0.3f, 0.1f, 1f);

        private Button button;
        private bool   isSelected;

        void Awake()
        {
            button = GetComponent<Button>();

            if (button == null)
                Debug.LogError($"Button tidak ditemukan pada {gameObject.name}");
        }

        /// <summary>Inisialisasi slot dengan data dan callback klik.</summary>
        public void Setup(string label, Sprite icon, string badge, Action onClick)
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button == null)
            {
                Debug.LogError($"Button masih null pada {gameObject.name}");
                return;
            }

            if (itemLabel != null)
                itemLabel.text = label;

            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.enabled = icon != null;
            }

            if (badgeText != null)
            {
                badgeText.text = badge;
                badgeText.gameObject.SetActive(!string.IsNullOrEmpty(badge));
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (background != null)
                background.color = selected ? selectedColor : normalColor;
        }
    }
}
