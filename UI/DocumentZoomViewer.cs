using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

namespace KKN.Game.UI
{
    /// <summary>
    /// Viewer zoom untuk dokumen. Mendukung:
    /// - Scroll mouse untuk zoom in/out
    /// - Klik & drag untuk pan
    /// - Tombol close / klik luar untuk tutup
    /// - Animasi fade in/out
    ///
    /// SETUP HIERARCHY (Tambahkan sebagai child dari Canvas):
    /// 
    /// [GameObject] "DocumentZoomViewer"   ← script ini
    ///   ├─ [Image] "Overlay"              ← background gelap semi-transparan, raycast on
    ///   ├─ [GameObject] "ViewerContainer" ← RectTransform, child dari overlay
    ///   │    └─ [Image] "DocumentImage"   ← gambar dokumen, dengan component ini
    ///   └─ [Button] "CloseButton"         ← tombol X (optional)
    ///        └─ [TMP_Text] "X"
    ///
    /// Pastikan DocumentZoomViewer punya CanvasGroup untuk animasi fade.
    /// </summary>
public class DocumentZoomViewer : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("Referensi UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image overlay;
        [SerializeField] private RectTransform documentImageRect;
        [SerializeField] private Image documentImage;
        [SerializeField] private Button closeButton;

        [Header("Zoom Settings")]
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 4f;
        [SerializeField] private float zoomStep = 0.15f;
        [SerializeField] private float zoomSmoothSpeed = 10f;

[Header("Animasi")]
        [SerializeField] private float fadeSpeed = 5f;

        // Step 5: Local sound effects
        [Header("Local Audio")]
        [SerializeField] private AudioSource localClickSound;
        [SerializeField] private AudioSource localZoomSound;

        // State
        private float currentZoom = 1f;
        private float targetZoom = 1f;
        private Vector2 lastPointerPosition;
        private bool isDragging = false;
        private bool isVisible = false;
        private Vector2 imageOriginPos;

        void Awake()
        {
            gameObject.SetActive(false);

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            // // Klik overlay tutup viewer
            // if (overlay != null)
            // {
            //     var overlayBtn = overlay.gameObject.AddComponent<Button>();
            //     overlayBtn.transition = Selectable.Transition.None;
            //     overlayBtn.onClick.AddListener(Hide);
            // }
        }

        public void OnOverlayClicked()
        {
            Hide();
        }

        void Update()
        {
            if (!isVisible) return;

            // Tekan Escape untuk tutup
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
                return;
            }

            HandleZoom();
            
            // Smooth zoom
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.unscaledDeltaTime * zoomSmoothSpeed);
            if (documentImageRect != null)
                documentImageRect.localScale = Vector3.one * currentZoom;

            // Clamp posisi agar tidak terlalu jauh dari layar
            ClampImagePosition();
        }

// =========================================
        // PUBLIC API
        // =========================================

        public void Show(Sprite sprite)
        {

            // Step 7: Error handling
            if (sprite == null)
            {
                Debug.LogWarning("[DocumentZoomViewer] Sprite is null!");
                return;
            }

            if (documentImage == null)
            {
                Debug.LogError("[DocumentZoomViewer] documentImage is null! Assign in Inspector.");
                return;
            }

            gameObject.SetActive(true);
            isVisible = true;

            documentImage.sprite = sprite;

            // Reset transform
            currentZoom = 1f;
            targetZoom = 1f;
            documentImageRect.localScale = Vector3.one;
            documentImageRect.anchoredPosition = Vector2.zero;
            imageOriginPos = Vector2.zero;

            StopAllCoroutines();
            StartCoroutine(FadeCanvasGroup(0f, 1f));

            // Step 5: Play local sound
            if (localClickSound != null)
            {
                localClickSound.Stop();
                localClickSound.Play();
            }

            Debug.Log("[DocumentZoomViewer] Show called");
        }

        private bool isClosing = false;
        public void Hide()
        {
            if (!isVisible || isClosing)
                return;

            isClosing = true;
            isVisible = false;

            StopAllCoroutines();

            StartCoroutine(
                FadeCanvasGroup(
                    canvasGroup.alpha,
                    0f,
                    () =>
                    {
                        gameObject.SetActive(false);
                        isClosing = false;

                        Debug.Log("[DocumentZoomViewer] Hide completed");
                    }
                )
            );
        }

// Step 5: Reset zoom on double-click
        public void OnPointerClick(PointerEventData eventData)
        {
            // Using Time.unscaledTime for double-click detection (more reliable)
            float currentTime = Time.unscaledTime;
            if (currentTime - lastClickTime < doubleClickThreshold)
            {
                // Double click detected - reset zoom
                targetZoom = 1f;
                documentImageRect.anchoredPosition = Vector2.zero;
                
                // Step 5: Play zoom sound
                if (localZoomSound != null)
                {
                    localZoomSound.Stop();
                    localZoomSound.Play();
                }
                
                Debug.Log("[DocumentZoomViewer] Double-click - zoom reset");
            }
            lastClickTime = currentTime;
        }

        // =========================================
        // POINTER EVENTS
        // =========================================

        void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetZoom += scroll * zoomStep;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

                if (localZoomSound != null)
                {
                    localZoomSound.Stop();
                    localZoomSound.Play();
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Hanya drag jika klik pada gambar dokumen, bukan overlay
            if (eventData.pointerEnter == documentImage.gameObject)
            {
                isDragging = true;
                lastPointerPosition = eventData.position;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isDragging = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            Vector2 delta = eventData.position - lastPointerPosition;
            lastPointerPosition = eventData.position;

            documentImageRect.anchoredPosition += delta;
        }

        // =========================================
        // HELPER
        // =========================================

        private void ClampImagePosition()
        {
            if (documentImageRect == null) return;

            // Batas geser berdasarkan zoom (semakin zoom in, semakin bebas geser)
            float halfW = documentImageRect.rect.width * currentZoom * 0.5f;
            float halfH = documentImageRect.rect.height * currentZoom * 0.5f;

            float clampX = Mathf.Max(0, halfW - Screen.width * 0.5f);
            float clampY = Mathf.Max(0, halfH - Screen.height * 0.5f);

            Vector2 pos = documentImageRect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -clampX, clampX);
            pos.y = Mathf.Clamp(pos.y, -clampY, clampY);
            documentImageRect.anchoredPosition = pos;
        }

        private IEnumerator FadeCanvasGroup(float from, float to, System.Action onComplete = null)
        {
            canvasGroup.alpha = from;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * fadeSpeed;
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            canvasGroup.alpha = to;
            onComplete?.Invoke();
        }

        // Tambah reset zoom dengan double click
        private float lastClickTime = 0f;
        private const float doubleClickThreshold = 0.3f;

        public void OnDoubleClickReset()
        {
            if (Time.unscaledTime - lastClickTime < doubleClickThreshold)
            {
                targetZoom = 1f;
                documentImageRect.anchoredPosition = Vector2.zero;
            }
            lastClickTime = Time.unscaledTime;
        }
    }
}