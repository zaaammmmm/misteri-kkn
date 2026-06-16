using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KKN.Game.UI
{
    public partial class DocumentSlotUI
    {
        /// <summary>
        /// Setup slot untuk item NON-dokumen (kunci, bahan, lainnya).
        /// Mengisi icon, quantity badge, dan callback klik.
        /// </summary>
        public void SetupGeneric(Sprite icon, string label, int quantity,
                                  System.Action onClick)
        {
            // Kosongkan bound document agar GetBoundDocument() return null
            // (menandakan ini bukan dokumen)
            boundDocument = null;

            if (iconImage != null)
                iconImage.sprite = icon;

            if (titleLabel != null)
                titleLabel.text = label;

            // Qty badge — cari TMP_Text bernama "Qty" di child
            var qtyTMP = transform.Find("Qty")?.GetComponent<TMP_Text>();
            if (qtyTMP != null)
                qtyTMP.text = quantity > 1 ? $"×{quantity}" : "";

            if (btn == null) btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
