using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KKN.Game.Systems;
using KKN.Game.Data;
using KKN.Game.Inventory;

namespace KKN.Game.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        public static PlayerHUD Instance { get; private set; }

        // ── Interact Prompt ───────────────────────────────
        [Header("Interact Prompt")]
        [SerializeField] private TMP_Text interactPromptText;
        [SerializeField] private CanvasGroup interactPromptGroup;

        // ── Objective ─────────────────────────────────────
        [Header("Objective")]
        [SerializeField] private GameObject objectivePanel;
        [SerializeField] private TMP_Text   objectiveText;
        [SerializeField] private float      objectiveHideDelay = 5f;

        // ── Pickup Notification ───────────────────────────
        [Header("Pickup Notification")]
        [SerializeField] private CanvasGroup pickupNotifGroup;
        [SerializeField] private Image       pickupIcon;
        [SerializeField] private TMP_Text    pickupNameText;
        [SerializeField] private TMP_Text    pickupCategoryText;
        [SerializeField] private float       notifDuration = 2.5f;
        [SerializeField] private float       notifFadeSpeed = 4f;

        // ── Stats Bars ────────────────────────────────────
        [Header("Stats Bars")]
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Slider sanitySlider;


        // ── Material Tracker ────────────────────────────────────
        [Header("Material Tracker")]
        [SerializeField] private Transform materialContainer;
        [SerializeField] private MaterialSlotUI materialSlotPrefab;

        private Dictionary<string, MaterialSlotUI> materialSlots =
            new Dictionary<string, MaterialSlotUI>();

        // ── Runtime ───────────────────────────────────────
        private Coroutine objectiveHideRoutine;
        private Coroutine notifRoutine;

        // ── Lifecycle ─────────────────────────────────────
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (InventorySystem.Instance != null)
            {
                InventorySystem.Instance.OnItemChanged -= HandleItemChanged;
                InventorySystem.Instance.OnDocumentAdded -= HandleDocumentAdded;
                InventorySystem.Instance.OnMaterialAdded -= HandleMaterialAdded;
            }

            if (ObjectiveManager.Instance != null)
                ObjectiveManager.Instance.OnObjectiveChanged -= ShowObjective;
        }

        void Start()
        {
            Debug.Log("PLAYER HUD START");
            
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnObjectiveChanged += ShowObjective;

                string current =
                    ObjectiveManager.Instance.GetCurrentObjective();

                if (!string.IsNullOrEmpty(current))
                    ShowObjective(current);
            }

            if (InventorySystem.Instance != null)
            {
                Debug.Log("SUBSCRIBE INVENTORY EVENTS");

                InventorySystem.Instance.OnItemChanged += HandleItemChanged;
                InventorySystem.Instance.OnDocumentAdded += HandleDocumentAdded;
                InventorySystem.Instance.OnMaterialAdded += HandleMaterialAdded;
            }

            RefreshMaterialTracker();

            if (pickupNotifGroup != null)
                pickupNotifGroup.alpha = 0f;

            HidePrompt();
        }

        // ══════════════════════════════════════════════════
        //  INTERACT PROMPT
        // ══════════════════════════════════════════════════

        public void ShowPrompt(string text)
        {
            if (string.IsNullOrEmpty(text)) { HidePrompt(); return; }

            if (interactPromptText  != null) interactPromptText.text = text;
            if (interactPromptGroup != null)
            {
                interactPromptGroup.alpha          = 1f;
                interactPromptGroup.gameObject.SetActive(true);
            }
        }

        public void HidePrompt()
        {
            if (interactPromptText  != null) interactPromptText.text = "";
            if (interactPromptGroup != null) interactPromptGroup.gameObject.SetActive(false);
        }

        // ══════════════════════════════════════════════════
        //  OBJECTIVE
        // ══════════════════════════════════════════════════

        public void ShowObjective(string text)
        {
            Debug.Log("PLAYER HUD SHOW OBJECTIVE: " + text);

            if (objectivePanel != null)
                objectivePanel.SetActive(true);
            else
                Debug.LogError("Objective Panel NULL");

            if (objectiveText != null)
                objectiveText.text = text;
            else
                Debug.LogError("Objective Text NULL");
        }

        IEnumerator HideObjectiveAfterDelay()
        {
            yield return new WaitForSecondsRealtime(objectiveHideDelay);
            if (objectivePanel != null) objectivePanel.SetActive(false);
        }

        // ══════════════════════════════════════════════════
        //  PICKUP NOTIFICATION (reaktif)
        // ══════════════════════════════════════════════════

        private void HandleItemChanged(string itemID, int newCount)
        {
            if (newCount <= 0) return; // item dihapus, tidak perlu notif

            var data = InventorySystem.Instance?.GetItemData(itemID);
            string displayName = data != null ? data.displayName : itemID;
            string category    = data != null ? data.itemType    : "Item";
            Sprite icon        = data?.icon;

            ShowPickupNotif(displayName, category, icon);
        }

        private void HandleDocumentAdded(DocumentData doc)
        {
            ShowPickupNotif(doc.title, "Dokumen", doc.icon);
        }

        private void HandleMaterialAdded(ItemData data, int count)
        {
            Debug.Log($"HUD MATERIAL RECEIVED: {data.displayName}");
            
            if (data == null)
                return;

            if (materialSlots.ContainsKey(data.itemID))
                return;

            var newSlot =
                Instantiate(materialSlotPrefab,
                materialContainer);

            Debug.Log(
                    $"CREATE SLOT : {data.displayName}");

            newSlot.Setup(
                data.itemID,
                data.icon);

            materialSlots.Add(data.itemID, newSlot);
        }

        private void RefreshMaterialTracker()
        {
            Debug.Log("REFRESH MATERIAL TRACKER");

            if (InventorySystem.Instance == null)
            {
                Debug.Log("InventorySystem NULL");
                return;
            }

            var materials =
                InventorySystem.Instance.GetItemsByType("Material");

            Debug.Log($"MATERIAL COUNT = {materials.Count}");

            foreach (var item in materials)
            {
                Debug.Log(
                    $"FOUND MATERIAL : {item.data.displayName}");

                HandleMaterialAdded(
                    item.data,
                    item.count);
            }
        }

        public void ShowPickupNotif(string itemName, string category, Sprite icon = null)
        {
            if (pickupNotifGroup == null) return;

            if (pickupIcon        != null) { pickupIcon.sprite  = icon; pickupIcon.enabled = icon != null; }
            if (pickupNameText    != null) pickupNameText.text    = itemName;
            if (pickupCategoryText != null) pickupCategoryText.text = $"[{category}]";

            if (notifRoutine != null) StopCoroutine(notifRoutine);
            notifRoutine = StartCoroutine(NotifFadeRoutine());
        }

        IEnumerator NotifFadeRoutine()
        {
            // Fade in
            pickupNotifGroup.gameObject.SetActive(true);
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.MoveTowards(t, 1f, notifFadeSpeed * Time.unscaledDeltaTime);
                pickupNotifGroup.alpha = t;
                yield return null;
            }

            // Hold
            yield return new WaitForSecondsRealtime(notifDuration);

            // Fade out
            while (pickupNotifGroup.alpha > 0f)
            {
                pickupNotifGroup.alpha -= notifFadeSpeed * Time.unscaledDeltaTime;
                yield return null;
            }

            pickupNotifGroup.gameObject.SetActive(false);
        }

        // ══════════════════════════════════════════════════
        //  STATS BARS (dipanggil dari PlayerState)
        // ══════════════════════════════════════════════════

        /// <param name="value">0–1</param>
        public void SetStamina(float value)
        {
            if (staminaSlider != null) staminaSlider.value = Mathf.Clamp01(value);
        }

        /// <param name="value">0–1</param>
        public void SetSanity(float value)
        {
            if (sanitySlider != null) sanitySlider.value = Mathf.Clamp01(value);
        }
    }
}
