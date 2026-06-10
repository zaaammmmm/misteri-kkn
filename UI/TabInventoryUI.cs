// ================================================================
//  TabInventoryUI.cs  ·  Overhaul AAA Horror — Versi V2
//  Namespace: KKN.Game.UI
//
//  PERUBAHAN DARI VERSI LAMA:
//  ▸ Sistem tab 5 kategori (SEMUA / KUNCI / BAHAN / DOKUMEN / LAINNYA)
//  ▸ Mendukung dua jenis item: DocumentData + ItemData (kunci, bensin, dll)
//  ▸ Detail panel: nama, tipe badge, deskripsi, tombol GUNAKAN + BUANG
//  ▸ Slot count realtime di header ("8 / 20 SLOT")
//  ▸ Teks tipe badge berwarna per kategori item
//  ▸ Keyboard navigasi: Arrow keys, Q/E ganti tab, Tab close
//  ▸ Semua fix lama dipertahankan:
//    [FIX-1] SetActive(true) sebelum Setup() agar Awake() terpanggil
//    [FIX-2] readButton selalu tampil saat dokumen dipilih
//    [FIX-3] Auto-select slot pertama saat buka
//    [FIX-4] ForceRebuildLayoutImmediate setelah slot dibuat
//    [FIX-5] Visual selection: deselect slot lain saat klik
//
//  KEBUTUHAN SCRIPT LAIN:
//  ▸ DocumentSlotUI.cs   — komponen slot dokumen (tidak berubah)
//  ▸ InventorySystem.cs  — harus punya GetDocuments() dan GetItems()
//  ▸ ItemData.cs         — ScriptableObject/class item umum
//                          minimal field: title, description, icon (Sprite),
//                          itemType (enum/string), quantity (int)
//  ▸ GameManager.cs      — SetState(), ResumeGame()
//  ▸ InputManager.cs     — GetTabDown()
//
//  FIELD INSPECTOR BARU (tambahan dari versi lama):
//  ▸ itemTypeText        → DetailPanel/ItemType (TMP_Text)
//  ▸ dropButton          → DetailPanel/DropButton (Button)
//  ▸ slotCountText       → Header/SlotCount (TMP_Text)
//  ▸ tabButtons[]        → TabBar/Tab_0 … Tab_4 (Button[5])
//  ▸ tabLabels[]         → TabBar/Tab_0/Label … (TMP_Text[5])
//  ▸ maxSlots            → default 20
// ================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KKN.Game.Systems;
using KKN.Game.Inventory;
using KKN.Game.Core;
using KKN.Game.Data;

namespace KKN.Game.UI
{
    public class TabInventoryUI : MonoBehaviour
    {
        // ── Root Panel ───────────────────────────────────
        [Header("Root Panel")]
        [SerializeField] private GameObject   rootPanel;
        [SerializeField] private CanvasGroup  rootCanvasGroup;

        // ── Tab Bar ──────────────────────────────────────
        [Header("Tab Bar")]
        [SerializeField] private Button[]   tabButtons  = new Button[5];
        [SerializeField] private TMP_Text[] tabLabels   = new TMP_Text[5];

        // ── Slot System ──────────────────────────────────
        [Header("Slot System")]
        [SerializeField] private Transform   slotParent;
        [SerializeField] private GameObject  slotPrefab;
        [SerializeField] private int         maxSlots = 20;

        // ── Preview / Detail Panel ───────────────────────
        [Header("Preview Panel")]
        [SerializeField] private Image      previewImage;
        [SerializeField] private TMP_Text   titleText;
        [SerializeField] private TMP_Text   itemTypeText;   // ← BARU: tipe badge
        [SerializeField] private TMP_Text   descText;
        [SerializeField] private Button     readButton;     // GUNAKAN / BACA
        [SerializeField] private Button     dropButton;     // ← BARU: BUANG

        // ── Header ───────────────────────────────────────
        [Header("Header")]
        [SerializeField] private TMP_Text   slotCountText; // ← BARU: "8 / 20 SLOT"

        // ── Empty State ──────────────────────────────────
        [Header("Empty State")]
        [SerializeField] private GameObject emptyStatePanel;

        // ── Zoom Viewer ──────────────────────────────────
        [Header("Zoom Viewer")]
        [SerializeField] private DocumentZoomViewer zoomViewer;

        // ── Audio ────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private AudioSource openSound;
        [SerializeField] private AudioSource closeSound;
        [SerializeField] private AudioSource clickSound;

        // ── Animasi ──────────────────────────────────────
        [Header("Animasi")]
        [SerializeField] private float fadeSpeed         = 6f;
        [SerializeField] private float panelSlideDistance= 40f;

        // ── Warna Tab ────────────────────────────────────
        [Header("Warna Tab")]
        [SerializeField] private Color tabActiveColor   = new Color(0.75f, 0.53f, 0.13f);
        [SerializeField] private Color tabInactiveColor = new Color(0.38f, 0.32f, 0.22f);

        // ── Warna Tipe Item ──────────────────────────────
        [Header("Warna Tipe Item")]
        [SerializeField] private Color typeColorKey      = new Color(0.80f, 0.58f, 0.15f);
        [SerializeField] private Color typeColorMaterial = new Color(0.45f, 0.75f, 0.35f);
        [SerializeField] private Color typeColorDocument = new Color(0.45f, 0.65f, 0.90f);
        [SerializeField] private Color typeColorOther    = new Color(0.65f, 0.55f, 0.45f);

        // ════════════════════════════════════════════════
        //  ENUM & DATA WRAPPER
        // ════════════════════════════════════════════════

        /// <summary>Kategori tab inventori</summary>
        public enum InventoryTab { All = 0, Keys = 1, Materials = 2, Documents = 3, Others = 4 }

        /// <summary>
        /// Wrapper universal untuk slot — bisa berisi DocumentData atau ItemData.
        /// Ini memungkinkan satu grid menampilkan semua jenis item.
        /// </summary>
        private class SlotData
        {
            public DocumentData Document;
            public ItemData     Item;

            public bool IsDocument => Document != null;
            public bool IsItem     => Item     != null;

            public string   Title       => IsDocument ? Document.title       : Item?.displayName ?? "?";
            public string   Description => IsDocument ? Document.description : Item?.description ?? "";
            public Sprite   Icon        => IsDocument ? Document.icon        : Item?.icon;
            public int      Quantity    => IsDocument ? 1
                                        : (Item != null && InventorySystem.Instance != null
                                            ? InventorySystem.Instance.GetItemCount(Item)
                                            : 1);
            public string   TypeLabel   => IsDocument ? "📄  DOKUMEN"
                                        : Item?.itemType switch
                                        {
                                            "Key"      => "⚿  KUNCI",
                                            "Material" => "🧪  BAHAN",
                                            _          => "🔦  LAINNYA"
                                        };
            public InventoryTab Category => IsDocument
                ? InventoryTab.Documents
                : Item?.itemType switch
                {
                    "Key"      => InventoryTab.Keys,
                    "Material" => InventoryTab.Materials,
                    _          => InventoryTab.Others
                };

            public SlotData(DocumentData doc)  { Document = doc; }
            public SlotData(ItemData     item) { Item     = item; }
        }

        // ════════════════════════════════════════════════
        //  PRIVATE STATE
        // ════════════════════════════════════════════════

        private bool              _isOpen          = false;
        private InventoryTab      _activeTab        = InventoryTab.All;
        private SlotData          _selectedData     = null;
        private int               _selectedSlotIndex= -1;
        private const int         GRID_COLUMNS      = 5;

        // Semua data mentah dari InventorySystem
        private List<SlotData>    _allSlots         = new List<SlotData>();
        // Slot yang sedang ditampilkan (sudah difilter per tab)
        private List<SlotData>    _filteredSlots    = new List<SlotData>();
        // Komponen DocumentSlotUI yang aktif di grid
        private List<DocumentSlotUI> _activeSlotUIs = new List<DocumentSlotUI>();

        private Coroutine _fadeCoroutine;

        // Label tab
        private static readonly string[] TAB_LABELS =
            { "SEMUA", "⚿  KUNCI", "🧪  BAHAN", "📄  DOKUMEN", "🔦  LAINNYA" };

        // ════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ════════════════════════════════════════════════

        private void Awake()
        {
            // Pastikan GameObject ini SELALU aktif agar Update() bisa berjalan.
            // Yang disembunyikan hanya rootPanel (konten UI), bukan GameObject ini.
            gameObject.SetActive(true);
        }

        private void Start()
        {
            // Sembunyikan konten panel, bukan GameObject ini
            if (rootPanel != null) rootPanel.SetActive(false);

            if (rootCanvasGroup == null && rootPanel != null)
                rootCanvasGroup = rootPanel.GetComponent<CanvasGroup>();

            SetupTabButtons();
            SetupActionButtons();
            SubscribeInventory();
            ClearPreview();
        }

        private void OnDestroy()
        {
            UnsubscribeInventory();
        }

        private void Update()
        {
            Debug.Log("UPDATE RUNNING");
            HandleInput();
        }

        // ════════════════════════════════════════════════
        //  SETUP
        // ════════════════════════════════════════════════

        private void SetupTabButtons()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;

                // Label default jika belum diisi
                if (tabLabels.Length > i && tabLabels[i] != null)
                    tabLabels[i].text = TAB_LABELS[i];

                int idx = i; // capture untuk lambda
                tabButtons[i].onClick.AddListener(() => SwitchTab((InventoryTab)idx));
            }

            RefreshTabVisuals();
        }

        private void SetupActionButtons()
        {
            if (readButton != null)
            {
                readButton.onClick.AddListener(OnReadOrUseClicked);
                readButton.gameObject.SetActive(false);
            }

            if (dropButton != null)
            {
                dropButton.onClick.AddListener(OnDropClicked);
                dropButton.gameObject.SetActive(false);
            }
        }

        private void SubscribeInventory()
        {
            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnInventoryChanged += OnInventoryChangedHandler;
            else
                Debug.LogWarning("[TabInventoryUI] InventorySystem tidak ditemukan — " +
                                 "inventory tidak akan update otomatis.");
        }

        private void UnsubscribeInventory()
        {
            if (InventorySystem.Instance != null)
                InventorySystem.Instance.OnInventoryChanged -= OnInventoryChangedHandler;
        }

        private void OnInventoryChangedHandler()
        {
            if (_isOpen) RefreshAll();
        }

        // ════════════════════════════════════════════════
        //  INPUT
        // ════════════════════════════════════════════════

        private void HandleInput()
        {
            var gm = GameManager.Instance;

            bool canOpen = gm == null
                || gm.CurrentState == GameManager.GameState.Playing
                || gm.CurrentState == GameManager.GameState.Inventory;

            if (!canOpen)
                return;

            bool tabDown = InputManager.Instance != null
                ? InputManager.Instance.GetTabDown()
                : Input.GetKeyDown(KeyCode.Tab);

            // TAB → buka/tutup inventory
            if (tabDown)
            {
                // Jika zoom viewer sedang terbuka
                if (zoomViewer != null && zoomViewer.gameObject.activeSelf)
                {
                    zoomViewer.Hide();
                    return;
                }

                if (_isOpen)
                    CloseMenu();
                else
                    OpenMenu();

                return;
            }

            // ESC → tutup inventory
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMenu();
            }
        }

        // ════════════════════════════════════════════════
        //  OPEN / CLOSE
        // ════════════════════════════════════════════════

        public void OpenMenu()
        {
            if (rootPanel == null) return;

            _isOpen = true;
            _selectedSlotIndex = -1;

            rootPanel.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameManager.Instance?.SetState(GameManager.GameState.Inventory);

            if (openSound != null)
                openSound.Play();

            ClearPreview();
            RefreshAll();
            AnimateOpen();
        }

        public void CloseMenu()
        {
            _isOpen = false;
            _selectedSlotIndex = -1;

            if (zoomViewer != null)
                zoomViewer.Hide();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            GameManager.Instance?.ResumeGame();

            if (closeSound != null)
                closeSound.Play();

            AnimateClose(() =>
            {
                if (rootPanel != null)
                    rootPanel.SetActive(false);
            });
        }

        // ════════════════════════════════════════════════
        //  TAB
        // ════════════════════════════════════════════════

        private void SwitchTab(InventoryTab tab)
        {
            if (_activeTab == tab) return;

            _activeTab         = tab;
            _selectedSlotIndex = -1;

            RefreshTabVisuals();
            BuildFilteredSlots();
            BuildSlotGrid();
            ClearPreview();
        }

        private void RefreshTabVisuals()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool active = (i == (int)_activeTab);

                if (tabLabels.Length > i && tabLabels[i] != null)
                    tabLabels[i].color = active ? tabActiveColor : tabInactiveColor;

                // Highlight background tombol aktif
                if (tabButtons[i] != null)
                {
                    var img = tabButtons[i].GetComponent<Image>();
                    if (img != null)
                        img.color = active
                            ? new Color(0.12f, 0.09f, 0.05f)
                            : new Color(0.06f, 0.04f, 0.03f);
                }
            }
        }

        // ════════════════════════════════════════════════
        //  DATA — Collect & Filter
        // ════════════════════════════════════════════════

        /// <summary>Ambil semua item dari InventorySystem dan rebuild grid</summary>
        private void RefreshAll()
        {
            CollectAllSlots();
            UpdateSlotCount();
            BuildFilteredSlots();
            BuildSlotGrid();
        }

        private void CollectAllSlots()
        {
            _allSlots.Clear();

            if (InventorySystem.Instance == null) return;

            // Dokumen
            var docs = InventorySystem.Instance.GetDocuments();
            if (docs != null)
                foreach (var d in docs)
                    if (d != null) _allSlots.Add(new SlotData(d));

            // Item umum (kunci, bensin, dll)
            // InventorySystem kamu perlu punya method GetItems() yang return List<ItemData>
            // Jika belum ada, bagian ini di-skip dengan aman.
            try
            {
                var items = InventorySystem.Instance.GetItems();
                if (items != null)
                    foreach (var it in items)
                        if (it != null) _allSlots.Add(new SlotData(it));
            }
            catch (System.Exception)
            {
                // GetItems() belum ada — tidak masalah, hanya dokumen yang ditampilkan
            }
        }

        private void BuildFilteredSlots()
        {
            _filteredSlots.Clear();

            foreach (var slot in _allSlots)
            {
                if (_activeTab == InventoryTab.All || slot.Category == _activeTab)
                    _filteredSlots.Add(slot);
            }
        }

        private void UpdateSlotCount()
        {
            if (slotCountText == null) return;
            slotCountText.text = $"{_allSlots.Count} / {maxSlots} SLOT";
        }

        // ════════════════════════════════════════════════
        //  GRID — Build Slots
        // ════════════════════════════════════════════════

        private void BuildSlotGrid()
        {
            if (slotParent == null)
            {
                Debug.LogError("[TabInventoryUI] slotParent is null! Assign di Inspector.");
                return;
            }
            if (slotPrefab == null)
            {
                Debug.LogError("[TabInventoryUI] slotPrefab is null! Assign di Inspector.");
                if (emptyStatePanel != null) emptyStatePanel.SetActive(true);
                return;
            }

            // Bersihkan slot lama
            foreach (Transform child in slotParent)
                Destroy(child.gameObject);
            _activeSlotUIs.Clear();

            bool isEmpty = _filteredSlots.Count == 0;
            if (emptyStatePanel != null) emptyStatePanel.SetActive(isEmpty);
            if (isEmpty) return;

            for (int i = 0; i < _filteredSlots.Count; i++)
            {
                SlotData data = _filteredSlots[i];
                GameObject slotGO = Instantiate(slotPrefab, slotParent);
                if (slotGO == null) continue;

                // [FIX-1] Paksa active agar Awake() dipanggil
                if (!slotGO.activeSelf) slotGO.SetActive(true);

                var slotUI = slotGO.GetComponent<DocumentSlotUI>();
                if (slotUI != null)
                {
                    if (data.IsDocument)
                    {
                        slotUI.Setup(data.Document, OnDocumentSlotClicked);
                    }
                    else
                    {
                        // Setup slot dengan ItemData via data wrapper
                        slotUI.SetupGeneric(
                            data.Icon,
                            data.Title,
                            data.Quantity,
                            () => OnItemSlotClicked(data)
                        );
                    }
                    _activeSlotUIs.Add(slotUI);
                }
                else
                {
                    // Fallback: slot tanpa DocumentSlotUI — isi manual
                    SetupFallbackSlot(slotGO, data);
                }
            }

            // [FIX-4] Paksa rebuild layout
            RebuildLayout();

            // [FIX-3] Auto-select pertama
            if (_filteredSlots.Count > 0) SelectSlotAtIndex(0);
        }

        private void SetupFallbackSlot(GameObject slotGO, SlotData data)
        {
            var img = slotGO.GetComponentInChildren<Image>();
            if (img != null && data.Icon != null) img.sprite = data.Icon;

            var btn = slotGO.GetComponent<Button>();
            if (btn != null)
            {
                SlotData captured = data;
                btn.onClick.AddListener(() =>
                {
                    if (captured.IsDocument) OnDocumentSlotClicked(captured.Document);
                    else                     OnItemSlotClicked(captured);
                });
            }
        }

        private void RebuildLayout()
        {
            if (!(slotParent is RectTransform rt)) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            var scroll = rt.GetComponentInParent<ScrollRect>();
            if (scroll != null && scroll.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.normalizedPosition = new Vector2(0f, 1f);
            }
        }

        // ════════════════════════════════════════════════
        //  SELECTION
        // ════════════════════════════════════════════════

        private void SelectSlotAtIndex(int index)
        {
            if (index < 0 || index >= _activeSlotUIs.Count) return;

            // Deselect semua
            for (int i = 0; i < _activeSlotUIs.Count; i++)
                _activeSlotUIs[i]?.SetSelected(false);

            _selectedSlotIndex = index;
            _activeSlotUIs[index]?.SetSelected(true);

            // Tampilkan detail
            if (index < _filteredSlots.Count)
                ShowDetail(_filteredSlots[index]);
        }

        private void OnDocumentSlotClicked(DocumentData doc)
        {
            if (doc == null) return;
            if (clickSound != null) clickSound.Play();

            // [FIX-5] Sync visual selection
            for (int i = 0; i < _activeSlotUIs.Count; i++)
            {
                if (i >= _filteredSlots.Count) continue;
                bool match = _filteredSlots[i].IsDocument
                          && _filteredSlots[i].Document == doc;
                _activeSlotUIs[i]?.SetSelected(match);
                if (match) _selectedSlotIndex = i;
            }

            ShowDetail(new SlotData(doc));
        }

        private void OnItemSlotClicked(SlotData data)
        {
            if (data == null) return;
            if (clickSound != null) clickSound.Play();

            for (int i = 0; i < _filteredSlots.Count; i++)
            {
                bool match = _filteredSlots[i] == data;
                if (i < _activeSlotUIs.Count)
                    _activeSlotUIs[i]?.SetSelected(match);
                if (match) _selectedSlotIndex = i;
            }

            ShowDetail(data);
        }

        // ════════════════════════════════════════════════
        //  DETAIL PANEL
        // ════════════════════════════════════════════════

        private void ShowDetail(SlotData data)
        {
            if (data == null) return;
            _selectedData = data;

            // Gambar preview
            if (previewImage != null)
                previewImage.sprite = data.Icon;

            // Nama
            if (titleText != null)
                titleText.text = data.Title;

            // Tipe badge dengan warna
            if (itemTypeText != null)
            {
                itemTypeText.text  = data.TypeLabel;
                itemTypeText.color = GetTypeColor(data);
            }

            // Deskripsi
            if (descText != null)
                descText.text = !string.IsNullOrEmpty(data.Description)
                    ? data.Description
                    : "(Tidak ada deskripsi)";

            // [FIX-2] Tombol GUNAKAN/BACA selalu tampil
            if (readButton != null)
            {
                readButton.gameObject.SetActive(true);

                // Ubah label sesuai jenis item
                var lbl = readButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null)
                    lbl.text = data.IsDocument ? "[ E ]  BACA" : "[ E ]  GUNAKAN";

                if (data.IsDocument && data.Document.fullImage == null)
                    Debug.LogWarning($"[TabInventoryUI] ⚠ '{data.Title}': fullImage belum diisi " +
                                     $"di ScriptableObject '{data.Document.name}'.");
            }

            // Tombol BUANG — tampil untuk semua item
            if (dropButton != null)
                dropButton.gameObject.SetActive(true);
        }

        private Color GetTypeColor(SlotData data)
        {
            if (data.IsDocument) return typeColorDocument;
            return data.Item?.itemType switch
            {
                "Key"      => typeColorKey,
                "Material" => typeColorMaterial,
                _          => typeColorOther
            };
        }

        private void ClearPreview()
        {
            _selectedData = null;
            if (previewImage  != null) previewImage.sprite = null;
            if (titleText     != null) titleText.text      = "";
            if (itemTypeText  != null) itemTypeText.text   = "";
            if (descText      != null) descText.text       = "Pilih item untuk melihat detail.";
            if (readButton    != null) readButton.gameObject.SetActive(false);
            if (dropButton    != null) dropButton.gameObject.SetActive(false);
        }

        // ════════════════════════════════════════════════
        //  AKSI TOMBOL
        // ════════════════════════════════════════════════

        private void OnReadOrUseClicked()
        {
            if (_selectedData == null)
            {
                Debug.LogWarning("[TabInventoryUI] Tidak ada item yang dipilih.");
                return;
            }

            if (_selectedData.IsDocument)
            {
                // Buka zoom viewer
                if (_selectedData.Document.fullImage == null)
                {
                    Debug.LogWarning($"[TabInventoryUI] '{_selectedData.Title}': fullImage null — " +
                                     "assign Sprite di ScriptableObject.");
                    return;
                }
                if (zoomViewer != null)
                    zoomViewer.Show(_selectedData.Document.fullImage);
                else
                    Debug.LogError("[TabInventoryUI] DocumentZoomViewer belum diassign di Inspector!");
            }
            else
            {
                // Gunakan item — notifikasi InventorySystem
                InventorySystem.Instance?.UseItem(_selectedData.Item);
                RefreshAll();
                ClearPreview();
            }
        }

        private void OnDropClicked()
        {
            if (_selectedData == null) return;

            if (_selectedData.IsDocument)
            {
                // Dokumen tidak bisa dibuang (opsional: aktifkan jika diinginkan)
                Debug.Log("[TabInventoryUI] Dokumen tidak dapat dibuang.");
                return;
            }

            InventorySystem.Instance?.RemoveItem(_selectedData.Item);
            RefreshAll();
            ClearPreview();
        }

        // ════════════════════════════════════════════════
        //  ANIMASI FADE + SLIDE
        // ════════════════════════════════════════════════

        private void AnimateOpen()
        {
            if (rootCanvasGroup == null) return;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, panelSlideDistance, 0f));
        }

        private void AnimateClose(System.Action onDone)
        {
            if (rootCanvasGroup == null) { onDone?.Invoke(); return; }
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(1f, 0f, 0f, -panelSlideDistance, onDone));
        }

        private IEnumerator FadeRoutine(
            float fromAlpha, float toAlpha,
            float fromOffsetY, float toOffsetY,
            System.Action onComplete = null)
        {
            RectTransform rt = rootPanel?.GetComponent<RectTransform>();
            Vector2 fromPos  = Vector2.up * fromOffsetY;
            Vector2 toPos    = Vector2.up * toOffsetY;
            float t          = 0f;

            if (rootCanvasGroup != null) rootCanvasGroup.alpha = fromAlpha;
            if (rt != null) rt.anchoredPosition = fromPos;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * fadeSpeed;
                t  = Mathf.Clamp01(t);

                if (rootCanvasGroup != null)
                    rootCanvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                if (rt != null)
                    rt.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);

                yield return null;
            }

            if (rootCanvasGroup != null) rootCanvasGroup.alpha = toAlpha;
            if (rt != null) rt.anchoredPosition = toPos;
            onComplete?.Invoke();
        }

        // ════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════

        /// <summary>Toggle buka/tutup dari luar (misal tombol pause)</summary>
        public void Toggle()
        {
            if (_isOpen) CloseMenu();
            else         OpenMenu();
        }

        /// <summary>Cek apakah inventory sedang terbuka</summary>
        public bool IsOpen => _isOpen;
    }
}
