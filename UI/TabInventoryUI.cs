using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using KKN.Game.Systems;
using KKN.Game.Data;
using KKN.Game.Inventory;

namespace KKN.Game.UI
{
    /// <summary>
    /// Inventory UI dengan tiga tab: Kunci, Bahan, Dokumen.
    /// Semua aksi menggunakan mouse (klik pada slot / tombol).
    /// Reaktif — subscribe ke InventorySystem.OnInventoryChanged.
    ///
    /// Hierarchy yang dibutuhkan:
    ///   InventoryCanvas (Canvas, CanvasGroup)
    ///     ├─ Background (Image, gelap semi-transparent)
    ///     ├─ TabBar
    ///     │    ├─ TabKey      (Button + TMP_Text "Kunci")
    ///     │    ├─ TabMaterial (Button + TMP_Text "Bahan")
    ///     │    └─ TabDocument (Button + TMP_Text "Dokumen")
    ///     ├─ SlotContainer   (GridLayoutGroup — slot item)
    ///     ├─ DetailPanel
    ///     │    ├─ DetailIcon   (Image)
    ///     │    ├─ DetailName   (TMP_Text)
    ///     │    ├─ DetailDesc   (TMP_Text)
    ///     │    └─ ActionButton (Button + TMP_Text — "Baca" / kosong)
    ///     └─ ItemSlotPrefab  (assign di Inspector, lihat SlotPrefab di bawah)
    /// </summary>
    public class TabInventoryUI : MonoBehaviour
    {
        // ── Tab enum ──────────────────────────────────────
        public enum InventoryTab { Key, Material, Document }

        // ── Inspector ─────────────────────────────────────
        [Header("Canvas")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject  inventoryRoot;

        [Header("Tabs")]
        [SerializeField] private Button  tabKeyButton;
        [SerializeField] private Button  tabMaterialButton;
        [SerializeField] private Button  tabDocumentButton;
        [SerializeField] private Color   tabActiveColor   = Color.white;
        [SerializeField] private Color   tabInactiveColor = new Color(0.5f, 0.5f, 0.5f);

        [Header("Slot Grid")]
        [SerializeField] private Transform  slotContainer;

        [Header("Prefabs")]
        [SerializeField] private GameObject itemSlotPrefab;
        [SerializeField] private GameObject documentSlotPrefab;

        [Header("Detail Panel")]
        [SerializeField] private GameObject  detailPanel;
        [SerializeField] private Image       detailIcon;
        [SerializeField] private TMP_Text    detailName;
        [SerializeField] private TMP_Text    detailDesc;
        [SerializeField] private Button      actionButton;
        [SerializeField] private TMP_Text    actionButtonLabel;

        [Header("Open / Close Animation Speed")]
        [SerializeField] private float animSpeed = 8f;

        [Header("Empty State")]
        [SerializeField] private TMP_Text emptyInventoryText;

        // ── Runtime ───────────────────────────────────────
        private InventoryTab currentTab = InventoryTab.Document;
        private bool         isOpen     = false;

        // Slot pool
        private readonly List<GameObject> slotPool = new();

        // Currently selected
        private string       selectedItemID  = null;
        private DocumentData selectedDoc     = null;

        // ── Lifecycle ─────────────────────────────────────
        void Awake()
        {
            // Tab buttons
            tabKeyButton     ?.onClick.AddListener(() => SwitchTab(InventoryTab.Key));
            tabMaterialButton?.onClick.AddListener(() => SwitchTab(InventoryTab.Material));
            tabDocumentButton?.onClick.AddListener(() => SwitchTab(InventoryTab.Document));

            // Action button (Baca dokumen)
            actionButton?.onClick.AddListener(OnActionButtonClicked);

            if (inventoryRoot != null) inventoryRoot.SetActive(false);
            if (canvasGroup   != null) canvasGroup.alpha = 0f;
        }

        void OnEnable()
        {
            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnInventoryChanged += RefreshCurrentTab;
        }

        void OnDisable()
        {
            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnInventoryChanged -= RefreshCurrentTab;
        }

        void Update()
        {
            // Tab keyboard shortcut
            if (Input.GetKeyDown(Core.GameConstants.KEY_TAB))
                ToggleInventory();

            // ESC tutup inventory
            if (isOpen && Input.GetKeyDown(Core.GameConstants.KEY_ESCAPE))
                CloseInventory();

            // Animasi alpha
            if (canvasGroup != null)
            {
                float target = isOpen ? 1f : 0f;
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, target, animSpeed * Time.unscaledDeltaTime);
                canvasGroup.interactable    = isOpen;
                canvasGroup.blocksRaycasts  = isOpen;
            }
        }

        // ══════════════════════════════════════════════════
        //  OPEN / CLOSE
        // ══════════════════════════════════════════════════

        public void ToggleInventory()
        {
            if (isOpen) CloseInventory();
            else        OpenInventory();
        }

        public void OpenInventory()
        {
            Debug.Log("OPEN");

            isOpen = true;

            inventoryRoot?.SetActive(true);

            Debug.Log("Inventory Root Active = " +
                (inventoryRoot != null && inventoryRoot.activeInHierarchy));

            Core.GameManager.Instance?.SetState(
                Core.GameManager.GameState.Inventory);

            SwitchTab(currentTab, true);
        }

        public void CloseInventory()
        {
            Debug.Log("CLOSE");

            isOpen = false;

            inventoryRoot?.SetActive(false);

            Core.GameManager.Instance?.SetState(
                Core.GameManager.GameState.Playing);

            detailPanel?.SetActive(false);
            if (detailName != null)
                detailName.text = "";

            if (detailDesc != null)
                detailDesc.text = "";
        }

        // ══════════════════════════════════════════════════
        //  TABS
        // ══════════════════════════════════════════════════

        public void SwitchTab(InventoryTab tab, bool forceRefresh = false)
        {
            if (currentTab == tab && !forceRefresh) return;

            currentTab     = tab;
            selectedItemID = null;
            selectedDoc    = null;
            detailPanel?.SetActive(false);

            UpdateTabColors();
            RefreshCurrentTab();
        }

        void UpdateTabColors()
        {
            SetTabColor(tabKeyButton,      currentTab == InventoryTab.Key);
            SetTabColor(tabMaterialButton, currentTab == InventoryTab.Material);
            SetTabColor(tabDocumentButton, currentTab == InventoryTab.Document);
        }

        void SetTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img) img.color = active ? tabActiveColor : tabInactiveColor;
        }

        // ══════════════════════════════════════════════════
        //  REFRESH SLOTS  (reaktif — dipanggil tiap ada perubahan)
        // ══════════════════════════════════════════════════

        public void RefreshCurrentTab()
        {
            if (!isOpen) return;

            ClearSlots();

            bool hasInventory = false;

            switch (currentTab)
            {
                case InventoryTab.Key:
                {
                    var list = InventorySystem.Instance.GetItemsByType("Key");
                    hasInventory = list.Count > 0;

                    PopulateItems("Key");
                    break;
                }

                case InventoryTab.Material:
                {
                    var list = InventorySystem.Instance.GetItemsByType("Material");
                    hasInventory = list.Count > 0;

                    PopulateItems("Material");
                    break;
                }

                case InventoryTab.Document:
                {
                    var docs = InventorySystem.Instance.GetAllDocuments();
                    hasInventory = docs.Count > 0;

                    PopulateDocuments();
                    break;
                }
            }

            ShowEmptyMessage(!hasInventory);
        }

        void PopulateItems(string itemType)
        {
            var list = InventorySystem.Instance.GetItemsByType(itemType);

            Debug.Log($"{itemType} Count = {list.Count}");

            foreach (var (data, count) in list)
            {
                Debug.Log($"ITEM : {data.displayName}");

                SpawnSlot(
                    data.displayName,
                    data.icon,
                    count > 1 ? $"×{count}" : "",
                    () => SelectItem(data.itemID));
            }
        }

        void PopulateDocuments()
        {
            if (InventorySystem.Instance == null)
            {
                Debug.LogError("InventorySystem NULL");
                return;
            }

            var docs = InventorySystem.Instance.GetAllDocuments();

            Debug.Log($"DOC COUNT = {docs.Count}");

            foreach (var doc in docs)
            {
                SpawnDocumentSlot(doc);
            }
        }

        void SpawnDocumentSlot(DocumentData doc)
        {
            if (documentSlotPrefab == null)
            {
                Debug.LogError("DocumentSlotPrefab NULL");
                return;
            }

            var go = Instantiate(documentSlotPrefab, slotContainer);

            Debug.Log("================================");
            Debug.Log("Prefab Name : " + documentSlotPrefab.name);
            Debug.Log("Spawned Name : " + go.name);

            Debug.Log("Button Root : " + go.GetComponent<Button>());
            Debug.Log("Button Child : " + go.GetComponentInChildren<Button>());
            Debug.Log("DocumentSlotUI : " + go.GetComponent<DocumentSlotUI>());

            foreach(var c in go.GetComponents<Component>())
            {
                Debug.Log("COMPONENT : " + c.GetType().Name);
            }

            var slot = go.GetComponent<DocumentSlotUI>();

            slot.Setup(doc, SelectDocument);

            slotPool.Add(go);
        }

        void SpawnSlot(string label, Sprite icon, string badge, System.Action onClick)
        {
            if (itemSlotPrefab == null)
            {
                Debug.LogError("ItemSlotPrefab NULL");
                return;
            }

            if (slotContainer == null)
            {
                Debug.LogError("SlotContainer NULL");
                return;
            }

            Debug.Log($"CREATE SLOT : {label}");

            var go = Instantiate(itemSlotPrefab, slotContainer);

            Debug.Log($"CREATED : {go.name}");

            var slot = go.GetComponent<InventorySlotUI>();

            if (slot == null)
            {
                Debug.LogError($"InventorySlotUI tidak ditemukan pada prefab {go.name}");
                Destroy(go);
                return;
            }

            slot.Setup(label, icon, badge, onClick);

            slotPool.Add(go);
        }
        void ClearSlots()
        {
            foreach (var go in slotPool)
            {
                if (go != null)
                    Destroy(go);
            }

            slotPool.Clear();
        }

        // ══════════════════════════════════════════════════
        //  SELECTION
        // ══════════════════════════════════════════════════

        void SelectItem(string itemID)
        {
            selectedItemID = itemID;
            selectedDoc    = null;

            var data = InventorySystem.Instance?.GetItemData(itemID);
            if (data == null) return;

            ShowDetail(data.displayName, data.description, data.icon, showAction: false);
        }

        void SelectDocument(DocumentData doc)
        {
            selectedDoc    = doc;
            selectedItemID = null;

            ShowDetail(doc.title, doc.description, doc.icon, showAction: true, actionLabel: "Baca");
        }

        void ShowDetail(string name, string desc, Sprite icon, bool showAction, string actionLabel = "")
        {
            if (detailPanel != null) detailPanel.SetActive(true);
            if (detailName  != null) detailName.text  = name;
            if (detailDesc  != null) detailDesc.text  = desc;
            if (detailIcon  != null) { detailIcon.sprite = icon; detailIcon.enabled = icon != null; }

            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(showAction);
                if (actionButtonLabel != null) actionButtonLabel.text = actionLabel;
            }
        }

        // ══════════════════════════════════════════════════
        //  ACTION BUTTON
        // ══════════════════════════════════════════════════

        void OnActionButtonClicked()
        {
            if (selectedDoc == null) return;

            // Buka DocumentZoomViewer
            DocumentZoomViewer.Instance?.Open(selectedDoc);
        }

        // ══════════════════════════════════════════════════
        //  HELPER - EMPTY STATE
        // ══════════════════════════════════════════════════
        void ShowEmptyMessage(bool show)
        {
            if (emptyInventoryText != null)
                emptyInventoryText.gameObject.SetActive(show);
        }
    }
}
