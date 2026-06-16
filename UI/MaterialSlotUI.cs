using UnityEngine;
using UnityEngine.UI;

namespace KKN.Game.UI
{
    public class MaterialSlotUI : MonoBehaviour
    {
        [SerializeField] private Image icon;

        public string ItemID { get; private set; }

        public void Setup(string itemID, Sprite sprite)
        {
            ItemID = itemID;

            if (icon != null)
                icon.sprite = sprite;
        }
    }
}