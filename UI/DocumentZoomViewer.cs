using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KKN.Game.Inventory;

namespace KKN.Game.UI
{
    /// <summary>
    /// Menampilkan dokumen full-screen dengan zoom dan pan menggunakan mouse.
    /// Dipanggil dari TabInventoryUI saat player klik "Baca".
    ///
    /// Hierarchy:
    ///   DocumentZoomCanvas
    ///     ├─ Overlay (Image, hitam transparan)
    ///     ├─ DocumentPanel
    ///     │    ├─ DocumentTitle   (TMP_Text)
    ///     │    ├─ DocumentContent (TMP_Text — teks dokumen)
    ///     │    ├─ DocumentImage   (Image — fullImage jika ada)
    ///     │    └─ CloseButton     (Button)
    ///     └─ ZoomHint             (TMP_Text — "Scroll: zoom | Drag: geser")
    /// </summary>
    public class DocumentZoomViewer : MonoBehaviour
    {
        public static DocumentZoomViewer Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject  viewerRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Document Elements")]
        [SerializeField] private TMP_Text  titleText;
        [SerializeField] private TMP_Text  contentText;
        [SerializeField] private Image     documentImage;

        [Header("Close")]
        [SerializeField] private Button    closeButton;

        [Header("Zoom & Pan Settings")]
        [SerializeField] private RectTransform documentPanel;   // panel yang di-zoom
        [SerializeField] private float          zoomMin      = 0.8f;
        [SerializeField] private float          zoomMax      = 3f;
        [SerializeField] private float          zoomSpeed    = 0.1f;
        [SerializeField] private float          animSpeed    = 10f;

        // ── Runtime ───────────────────────────────────────
        private bool    isOpen    = false;
        private float   zoomTarget = 1f;
        private Vector2 panOffset  = Vector2.zero;
        private Vector2 panTarget  = Vector2.zero;

        // Pan drag
        private bool    isDragging    = false;
        private Vector2 lastMousePos;

        // ── Lifecycle ─────────────────────────────────────
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            closeButton?.onClick.AddListener(Close);

            if (viewerRoot != null) viewerRoot.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        void Update()
        {
            if (!isOpen) return;

            HandleZoom();
            HandlePan();
            ApplyTransform();

            // Tutup dengan ESC (kembali ke inventory, bukan ke game)
            if (Input.GetKeyDown(Core.GameConstants.KEY_ESCAPE))
                Close();
        }

        // ══════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════

        public void Open(DocumentData doc)
        {
            if (doc == null) return;

            // Reset transform
            zoomTarget  = 1f;
            panTarget   = Vector2.zero;
            panOffset   = Vector2.zero;

            // Isi konten
            if (titleText    != null) titleText.text    = doc.title;
            if (contentText  != null) contentText.text  = doc.description;

            bool hasImage = doc.fullImage != null;
            if (documentImage != null)
            {
                documentImage.gameObject.SetActive(hasImage);
                if (hasImage) documentImage.sprite = doc.fullImage;
            }
            if (contentText  != null) contentText.gameObject.SetActive(!hasImage || string.IsNullOrEmpty(doc.description) == false);

            isOpen = true;
            viewerRoot?.SetActive(true);

            // Inventory tetap aktif di bawah; hanya layer ini yang ditampilkan
        }

        public void Close()
        {
            isOpen = false;
            viewerRoot?.SetActive(false);
        }

        // ══════════════════════════════════════════════════
        //  ZOOM & PAN
        // ══════════════════════════════════════════════════

        void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                zoomTarget = Mathf.Clamp(zoomTarget + scroll * zoomSpeed * 10f, zoomMin, zoomMax);

                // Jika zoom kembali ke 1, snap pan ke pusat
                if (zoomTarget <= zoomMin + 0.05f) panTarget = Vector2.zero;
            }
        }

        void HandlePan()
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDragging   = true;
                lastMousePos = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(0))
                isDragging = false;

            if (isDragging)
            {
                Vector2 delta = (Vector2)Input.mousePosition - lastMousePos;
                lastMousePos  = Input.mousePosition;
                panTarget    += delta / documentPanel.lossyScale.x;
            }
        }

        void ApplyTransform()
        {
            if (documentPanel == null) return;

            // Smooth zoom
            float currentZoom = documentPanel.localScale.x;
            float newZoom     = Mathf.Lerp(currentZoom, zoomTarget, animSpeed * Time.unscaledDeltaTime);
            documentPanel.localScale = new Vector3(newZoom, newZoom, 1f);

            // Smooth pan — batasi pan agar dokumen tidak keluar layar
            float halfW   = documentPanel.rect.width  * newZoom * 0.5f;
            float halfH   = documentPanel.rect.height * newZoom * 0.5f;
            float maxPanX = Mathf.Max(0f, halfW  - Screen.width  * 0.5f);
            float maxPanY = Mathf.Max(0f, halfH  - Screen.height * 0.5f);

            panTarget.x  = Mathf.Clamp(panTarget.x,  -maxPanX, maxPanX);
            panTarget.y  = Mathf.Clamp(panTarget.y,  -maxPanY, maxPanY);

            panOffset = Vector2.Lerp(panOffset, panTarget, animSpeed * Time.unscaledDeltaTime);
            documentPanel.anchoredPosition = panOffset;

            // Alpha fade in
            if (canvasGroup != null && canvasGroup.alpha < 1f)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, animSpeed * Time.unscaledDeltaTime);
        }
    }
}
