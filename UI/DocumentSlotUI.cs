using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KKN.Game.Inventory;

namespace KKN.Game.UI
{
    /// <summary>
    /// Komponen untuk setiap slot dokumen di Tab Inventory.
    /// 
    /// SETUP PREFAB SLOT:
    /// - GameObject "DocumentSlot"
    ///   |- Image (component: Image) — background slot
    ///   |- Button (component: Button)
    ///   |- Image "IconImage" — isi sprite icon dokumen
    ///   |- GameObject "HoverGlow" — efek highlight saat hover (optional)
    ///   |- TMP_Text "TitleLabel" — judul singkat (optional)
    ///   Tambahkan script ini ke root GameObject slot.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public partial class DocumentSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
[Header("Referensi Visual")]
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject hoverGlow;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private GameObject newBadge; // Step 8: NEW badge for unpicked

        [Header("Audio")]
        [SerializeField] private AudioSource hoverSound; // Step 6: Hover sound
        [SerializeField] private AudioSource clickSound; // Step 6: Click sound

        [Header("Animasi")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float animSpeed = 8f;

        // Step 6: Selection State
        [Header("Selection State")]
        [SerializeField] private bool isSelected = false;
        [SerializeField] private Color selectedColor = new Color(0.9f, 0.8f, 0.4f, 0.4f);
        [SerializeField] private Color normalColor = new Color(0.14f, 0.12f, 0.08f, 0.9f);

        private DocumentData boundDocument;
        private System.Action<DocumentData> onClickCallback;
        private Vector3 originalScale;
        private Vector3 targetScale;
        private Button btn;
        private Image backgroundImage;

void Awake()
        {
            // FIX: Add null check to prevent NullReferenceException
            btn = GetComponent<Button>();
            if (btn == null)
            {
                // Try to add Button component if missing
                btn = gameObject.AddComponent<Button>();
                Debug.LogWarning("[DocumentSlotUI] Button component was missing, added automatically.");
            }
            
            backgroundImage = GetComponent<Image>();
            originalScale = transform.localScale;
            targetScale = originalScale;

            if (hoverGlow != null)
                hoverGlow.SetActive(false);

            // Step 8: Hide new badge by default
            if (newBadge != null)
                newBadge.SetActive(false);
        }

        void Update()
        {
            // Smooth scale animation
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animSpeed);
        }

/// <summary>
        /// Bind data dokumen ke slot ini.
        /// </summary>
        public void Setup(DocumentData doc, System.Action<DocumentData> onClick)
        {
            // Step 7: Add null check for doc parameter
            if (doc == null)
            {
                Debug.LogWarning("[DocumentSlotUI] Document data is null! Cannot setup slot.");
                return;
            }

            boundDocument = doc;
            onClickCallback = onClick;

            // FIX: Add null checks for all UI references to prevent NullReferenceException
            if (iconImage != null)
            {
                if (doc.icon != null)
                    iconImage.sprite = doc.icon;
                else
                    iconImage.sprite = null;
            }
            else
            {
                Debug.LogWarning("[DocumentSlotUI] iconImage is null! Please assign in slot prefab.");
            }

            if (titleLabel != null)
                titleLabel.text = doc.title;
            else
                Debug.LogWarning("[DocumentSlotUI] titleLabel is null! Please assign in slot prefab.");

            // Step 8: Show NEW badge if document hasn't been read (with null check)
            if (newBadge != null)
                newBadge.SetActive(!doc.isPicked);

            // FIX: Add null check for btn to prevent NullReferenceException
            if (btn == null)
            {
                Debug.LogError("[DocumentSlotUI] Button component is null! Please ensure slot prefab has a Button component.");
                return;
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => 
            {
                // Step 6: Play click sound
                if (clickSound != null)
                    clickSound.Play();
                
                // Always invoke callback if it exists
                if (onClickCallback != null)
                    onClickCallback?.Invoke(boundDocument);
                
                // Step 8: Mark document as picked when clicked
                if (boundDocument != null)
                    boundDocument.isPicked = true;
            });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = originalScale * hoverScale;
            if (hoverGlow != null) hoverGlow.SetActive(true);
            
            // Step 6: Play hover sound
            if (hoverSound != null)
            {
                hoverSound.Stop();
                hoverSound.Play();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = originalScale;
            if (hoverGlow != null) hoverGlow.SetActive(false);
        }

        // Step 3: Get bound document for keyboard navigation
        public DocumentData GetBoundDocument()
        {
            return boundDocument;
        }

        // Step 6: Set selection state
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (backgroundImage != null)
                backgroundImage.color = selected ? selectedColor : normalColor;
            
            Debug.Log($"[DocumentSlotUI] SetSelected: {selected}");
        }

        // Step 6: Get selection state
        public bool IsSelected()
        {
            return isSelected;
        }
    }
}
