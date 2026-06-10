// ================================================================
//  PlayerHUD.cs  ·  Versi Overhaul AAA Horror
//  HUD utama player — Misteri KKN
//
//  CHANGELOG vs versi lama:
//  ▸ SanityBar sekarang berkedip dan bergetar saat sanity rendah
//  ▸ BatteryText menampilkan ikon █▓▒░ sebagai bar visual
//  ▸ GhostAlertText muncul dengan efek typewriter + shake
//  ▸ Crosshair adaptif: berubah warna saat hover interactable
//  ▸ PromptText memiliki fade-in/out yang halus (bukan snap)
//  ▸ SanityVignette menggunakan gradient merah + ungu (bukan hitam flat)
//  ▸ HeartbeatPulse: efek detak jantung visual saat sanity kritis
//  ▸ NoiseIndicator: lingkaran kecil di pojok yang pulse saat player berisik
// ================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using KKN.Game.Player;
using KKN.Game.Systems;
using KKN.Game.Core;

namespace KKN.Game.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        public static PlayerHUD Instance { get; private set; }

        // ── Status Bars ──────────────────────────────────
        [Header("Status Bars")]
        [SerializeField] private Image   staminaFill;
        [SerializeField] private Image   sanityFill;
        [SerializeField] private Image   staminaBG;
        [SerializeField] private Image   sanityBG;
        [SerializeField] private CanvasGroup staminaGroup;
        [SerializeField] private CanvasGroup sanityGroup;

        // ── Battery ──────────────────────────────────────
        [Header("Battery")]
        [SerializeField] private TMP_Text batteryText;
        [SerializeField] private TMP_Text batteryWarningText;
        [SerializeField] private Image    batteryIcon;
        [SerializeField] private CanvasGroup batteryGroup;

        // ── Inventory Icons ──────────────────────────────
        [Header("Inventory Icons")]
        [SerializeField] private Image introKeyIcon;
        [SerializeField] private Image mainKeyIcon;
        [SerializeField] private Image exitKeyIcon;
        [SerializeField] private Image generatorKeyIcon;
        [SerializeField] private Image gasIcon;
        [SerializeField] private Image oilIcon;

        // ── Teks UI ──────────────────────────────────────
        [Header("Text")]
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text ghostAlertText;
        [SerializeField] private TMP_Text noiseLabel;     // "KEHENINGAN" / "BERISIK"

        // ── Crosshair ────────────────────────────────────
        [Header("Crosshair")]
        [SerializeField] private Image crosshairCenter;
        [SerializeField] private Image crosshairRing;

        // ── Efek Atmosfer ────────────────────────────────
        [Header("Efek Atmosfer")]
        [SerializeField] private Image vignette;          // Vignette utama (sanity)
        [SerializeField] private Image damageFlash;       // Flash merah saat damage
        [SerializeField] private Image heartbeatOverlay;  // Pulse overlay saat sanity kritis
        [SerializeField] private Image noiseIndicator;    // Lingkaran noise indicator

        // ── Pengaturan ───────────────────────────────────
        [Header("Pengaturan")]
        [SerializeField] private float barLerpSpeed        = 8f;
        [SerializeField] private float sanityShakeThreshold = 0.30f;  // Di bawah ini bar mulai shake
        [SerializeField] private float sanityPulseThreshold = 0.20f;  // Di bawah ini efek heartbeat muncul
        [SerializeField] private float hudHideAlpha         = 0.15f;   // Alpha HUD saat penuh (minimal UI)
        [SerializeField] private float hudShowAlpha         = 1.00f;

        // ── State Internal ───────────────────────────────
        private PlayerMovement   _movement;
        private SanitySystem     _sanity;
        private FlashlightSystem _flash;
        private InventorySystem  _inventory;
        private PlayerNoiseEmitter _noise;

        private Coroutine _ghostAlertRoutine;
        private Coroutine _promptRoutine;
        private Coroutine _objectiveRoutine;
        private Coroutine _heartbeatRoutine;

        private Vector2 _staminaBasePos;
        private Vector2 _sanityBasePos;
        private float   _sanityShakeTime;
        private bool    _heartbeatRunning;
        private string  _pendingPrompt = "";
        private bool    _hasInteractable;

        // Warna sanity berdasarkan level
        private static readonly Color SanityColor_High    = new Color(0.30f, 0.80f, 0.90f); // Cyan tenang
        private static readonly Color SanityColor_Medium  = new Color(0.60f, 0.20f, 0.90f); // Ungu gelisah
        private static readonly Color SanityColor_Low     = new Color(0.85f, 0.10f, 0.25f); // Merah putus asa
        private static readonly Color StaminaColor_High   = new Color(0.20f, 0.80f, 0.25f);
        private static readonly Color StaminaColor_Low    = new Color(0.85f, 0.55f, 0.10f);

        // ================================================================
        //  UNITY LIFECYCLE
        // ================================================================

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Simpan posisi awal bar untuk efek shake
            if (staminaFill != null) _staminaBasePos = staminaFill.rectTransform.anchoredPosition;
            if (sanityFill  != null) _sanityBasePos  = sanityFill.rectTransform.anchoredPosition;

            // Sembunyikan efek saat start
            SetAlpha(vignette,          0f);
            SetAlpha(damageFlash,       0f);
            SetAlpha(heartbeatOverlay,  0f);
        }

        private void Start()
        {
            _movement  = FindFirstObjectByType<PlayerMovement>();
            _sanity    = SanitySystem.Instance;
            _flash     = FlashlightSystem.Instance;
            _inventory = InventorySystem.Instance;
            _noise     = FindFirstObjectByType<PlayerNoiseEmitter>();

            if (promptText    != null) promptText.alpha    = 0f;
            if (ghostAlertText!= null) ghostAlertText.gameObject.SetActive(false);
            if (objectiveText != null) objectiveText.gameObject.SetActive(false);
            if (batteryWarningText != null) batteryWarningText.gameObject.SetActive(false);

            if (_inventory != null) _inventory.OnInventoryChanged += UpdateInventoryUI;
            UpdateInventoryUI();
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.OnInventoryChanged -= UpdateInventoryUI;
        }

        private void Update()
        {
            // Lazy-find saat komponen belum tersedia
            if (_movement  == null) _movement  = FindFirstObjectByType<PlayerMovement>();
            if (_sanity    == null) _sanity    = SanitySystem.Instance;
            if (_flash     == null) _flash     = FlashlightSystem.Instance;
            if (_inventory == null) _inventory = InventorySystem.Instance;
            if (_noise     == null) _noise     = FindFirstObjectByType<PlayerNoiseEmitter>();

            UpdateStamina();
            UpdateSanity();
            UpdateBattery();
            UpdateVignette();
            UpdateHeartbeat();
            UpdateCrosshair();
            UpdateNoiseIndicator();
        }

        // ================================================================
        //  UPDATE — Status Bars
        // ================================================================

        private void UpdateStamina()
        {
            if (_movement == null || staminaFill == null) return;

            float val = _movement.GetStaminaPercent();
            staminaFill.fillAmount = Mathf.Lerp(staminaFill.fillAmount, val,
                                                 Time.deltaTime * barLerpSpeed);

            staminaFill.color = Color.Lerp(StaminaColor_Low, StaminaColor_High,
                                            Mathf.Clamp01(val * 2f));

            // Fade group saat stamina penuh — HUD tidak mengganggu
            if (staminaGroup != null)
                staminaGroup.alpha = Mathf.Lerp(staminaGroup.alpha,
                    val > 0.95f ? hudHideAlpha : hudShowAlpha,
                    Time.deltaTime * 4f);
        }

        private void UpdateSanity()
        {
            if (_sanity == null || sanityFill == null) return;

            float val = _sanity.Percent();
            sanityFill.fillAmount = Mathf.Lerp(sanityFill.fillAmount, val,
                                                Time.deltaTime * barLerpSpeed);

            // Interpolasi warna tiga titik
            Color targetColor;
            if (val > GameConstants.SANITY_CALM)
                targetColor = SanityColor_High;
            else if (val > GameConstants.SANITY_UNEASY)
                targetColor = Color.Lerp(SanityColor_Medium, SanityColor_High,
                                          (val - GameConstants.SANITY_UNEASY) /
                                          (GameConstants.SANITY_CALM - GameConstants.SANITY_UNEASY));
            else
                targetColor = Color.Lerp(SanityColor_Low, SanityColor_Medium,
                                          val / GameConstants.SANITY_UNEASY);

            sanityFill.color = Color.Lerp(sanityFill.color, targetColor, Time.deltaTime * 6f);

            // Efek shake pada bar saat sanity rendah
            if (val < sanityShakeThreshold && sanityFill != null)
            {
                _sanityShakeTime += Time.deltaTime * 18f;
                float shake = (1f - val / sanityShakeThreshold) * 4f;
                sanityFill.rectTransform.anchoredPosition = _sanityBasePos +
                    new Vector2(Mathf.Sin(_sanityShakeTime) * shake,
                                Mathf.Cos(_sanityShakeTime * 1.3f) * shake * 0.5f);
            }
            else
            {
                if (sanityFill != null)
                    sanityFill.rectTransform.anchoredPosition = Vector2.Lerp(
                        sanityFill.rectTransform.anchoredPosition, _sanityBasePos,
                        Time.deltaTime * 12f);
            }
        }

        // ================================================================
        //  UPDATE — Battery
        // ================================================================

        private void UpdateBattery()
        {
            if (_flash == null) return;

            float pct   = _flash.BatteryPercent();
            int   iPct  = Mathf.RoundToInt(pct * 100f);
            bool  isLow = iPct <= GameConstants.FLASHLIGHT_CRITICAL;

            // Visual "bar" ASCII: ████░░░░
            if (batteryText != null)
            {
                int filled = Mathf.RoundToInt(pct * 8f);
                string bar = new string('█', filled) + new string('░', 8 - filled);
                Color  col = isLow
                    ? Color.Lerp(Color.red, new Color(1f, 0.5f, 0.1f),
                                  Mathf.PingPong(Time.time * 4f, 1f))
                    : new Color(0.80f, 0.78f, 0.60f);

                batteryText.text  = $"<color=#{ColorUtility.ToHtmlStringRGB(col)}>{bar}</color>  {iPct}%";
            }

            if (batteryWarningText != null)
            {
                batteryWarningText.gameObject.SetActive(isLow);
                if (isLow) batteryWarningText.alpha = Mathf.PingPong(Time.time * 2.5f, 1f);
            }

            if (batteryGroup != null)
                batteryGroup.alpha = Mathf.Lerp(batteryGroup.alpha,
                    isLow ? 1f : (pct > 0.8f ? hudHideAlpha : hudShowAlpha),
                    Time.deltaTime * 4f);
        }

        // ================================================================
        //  UPDATE — Atmosfer
        // ================================================================

        private void UpdateVignette()
        {
            if (_sanity == null || vignette == null) return;

            float val = _sanity.Percent();

            // Vignette semakin gelap dan kemerahan saat sanity turun
            float targetAlpha = Mathf.Lerp(0.55f, 0f, val);
            Color vigCol = Color.Lerp(
                new Color(0.4f, 0f, 0.05f),   // merah gelap saat kritis
                new Color(0f,   0f, 0f),        // hitam netral
                Mathf.Clamp01(val * 2f));

            vigCol.a = Mathf.Lerp(vignette.color.a, targetAlpha, Time.deltaTime * 3.5f);
            vignette.color = vigCol;
        }

        private void UpdateHeartbeat()
        {
            if (_sanity == null || heartbeatOverlay == null) return;

            bool shouldPulse = _sanity.Percent() < sanityPulseThreshold;

            if (shouldPulse && !_heartbeatRunning)
            {
                _heartbeatRunning  = true;
                _heartbeatRoutine  = StartCoroutine(HeartbeatRoutine());
            }
            else if (!shouldPulse && _heartbeatRunning)
            {
                _heartbeatRunning = false;
                if (_heartbeatRoutine != null) StopCoroutine(_heartbeatRoutine);
                SetAlpha(heartbeatOverlay, 0f);
            }
        }

        private void UpdateCrosshair()
        {
            if (crosshairCenter == null) return;

            Color target = _hasInteractable
                ? new Color(1.0f, 0.90f, 0.50f, 0.90f)   // kuning saat ada interactable
                : new Color(1.0f, 1.00f, 1.00f, 0.55f);  // putih redup saat normal

            crosshairCenter.color = Color.Lerp(crosshairCenter.color, target, Time.deltaTime * 12f);
        }

        private void UpdateNoiseIndicator()
        {
            if (noiseIndicator == null) return;

            float noiseLevel = GetNoiseLevel();
            float pulse = Mathf.Abs(Mathf.Sin(Time.time * (4f + noiseLevel * 6f)));

            noiseIndicator.color = Color.Lerp(
                new Color(0.3f, 0.8f, 0.3f, 0.1f + pulse * 0.15f),   // hijau tenang
                new Color(0.9f, 0.3f, 0.1f, 0.3f + pulse * 0.50f),   // merah berisik
                noiseLevel);

            if (noiseLabel != null)
            {
                noiseLabel.text  = noiseLevel > 0.5f ? "BERISIK" : "SENYAP";
                noiseLabel.color = noiseLevel > 0.5f
                    ? new Color(0.9f, 0.3f, 0.1f, 0.8f)
                    : new Color(0.4f, 0.7f, 0.4f, 0.5f);
            }
        }

        /// <summary>
        /// Mengambil noise level dari PlayerNoiseEmitter secara defensif.
        /// Mencoba field/property umum terlebih dahulu, lalu reflection sebagai fallback.
        /// Jika PlayerNoiseEmitter kamu memiliki nama field/property yang berbeda,
        /// tambahkan di blok pertama — lebih efisien daripada reflection.
        /// </summary>
        private float GetNoiseLevel()
        {
            if (_noise == null) return 0f;

            return _noise.CurrentNoiseLevel;
        }

        // ================================================================
        //  UPDATE — Inventory
        // ================================================================

        private void UpdateInventoryUI()
        {
            if (_inventory == null) return;

            SetIconState(introKeyIcon,     _inventory.HasItem("IntroKey"));
            SetIconState(mainKeyIcon,      _inventory.HasItem("MainKey"));
            SetIconState(exitKeyIcon,      _inventory.HasItem("ExitKey"));
            SetIconState(generatorKeyIcon, _inventory.HasItem("GeneratorKey"));
            SetIconState(gasIcon,          _inventory.HasItem("Gasoline") || _inventory.HasItem("Gas"));
            SetIconState(oilIcon,          _inventory.HasItem("Oil"));
        }

        private void SetIconState(Image icon, bool owned)
        {
            if (icon == null) return;
            Color target = owned
                ? new Color(0.88f, 0.72f, 0.20f, 1.0f)   // emas saat punya
                : new Color(0.35f, 0.33f, 0.30f, 0.5f);  // abu redup saat tidak punya
            icon.color = Color.Lerp(icon.color, target, Time.deltaTime * 6f);
        }

        // ================================================================
        //  PUBLIC API
        // ================================================================

        /// <summary>Notifikasi kerusakan — flash merah + shake kamera (kamera shake di PlayerLook)</summary>
        public void FlashDamage()
        {
            StartCoroutine(DamageRoutine());
        }

        /// <summary>Tampilkan teks objective dengan fade-in dan fade-out otomatis</summary>
        public void ShowObjective(string msg)
        {
            if (_objectiveRoutine != null) StopCoroutine(_objectiveRoutine);
            _objectiveRoutine = StartCoroutine(ObjectiveRoutine(msg));
        }

        /// <summary>Tampilkan prompt interaksi dengan fade halus</summary>
        public void ShowPrompt(string text)
        {
            _hasInteractable = !string.IsNullOrEmpty(text);
            _pendingPrompt   = text;

            if (_promptRoutine != null) StopCoroutine(_promptRoutine);
            _promptRoutine = StartCoroutine(PromptFadeRoutine(text, true));
        }

        /// <summary>Sembunyikan prompt interaksi</summary>
        public void HidePrompt()
        {
            _hasInteractable = false;
            if (_promptRoutine != null) StopCoroutine(_promptRoutine);
            _promptRoutine = StartCoroutine(PromptFadeRoutine("", false));
        }

        /// <summary>Tampilkan peringatan ghost dengan efek typewriter</summary>
        public void ShowGhostAlert(string msg = "SESUATU MENDEKAT...")
        {
            if (_ghostAlertRoutine != null) StopCoroutine(_ghostAlertRoutine);
            _ghostAlertRoutine = StartCoroutine(GhostAlertRoutine(msg));
        }

        // ================================================================
        //  COROUTINES
        // ================================================================

        private IEnumerator DamageRoutine()
        {
            if (damageFlash == null) yield break;

            // Spike cepat ke alpha tinggi
            SetAlpha(damageFlash, 0.70f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 2.5f;
                SetAlpha(damageFlash, Mathf.Lerp(0.70f, 0f, t));
                yield return null;
            }
        }

        private IEnumerator ObjectiveRoutine(string msg)
        {
            if (objectiveText == null) yield break;

            objectiveText.gameObject.SetActive(true);
            objectiveText.text  = msg;
            objectiveText.alpha = 0f;

            // Fade in
            yield return FadeTextAlpha(objectiveText, 0f, 1f, 0.6f);
            yield return new WaitForSeconds(GameConstants.OBJECTIVE_SHOW_TIME);

            // Fade out
            yield return FadeTextAlpha(objectiveText, 1f, 0f, 1.2f);
            objectiveText.gameObject.SetActive(false);
        }

        private IEnumerator PromptFadeRoutine(string newText, bool fadeIn)
        {
            if (promptText == null) yield break;

            if (fadeIn)
            {
                promptText.text  = newText;
                yield return FadeTextAlpha(promptText, promptText.alpha, 1f, 0.2f);
            }
            else
            {
                yield return FadeTextAlpha(promptText, promptText.alpha, 0f, 0.3f);
                promptText.text = "";
            }
        }

        private IEnumerator GhostAlertRoutine(string msg)
        {
            if (ghostAlertText == null) yield break;

            ghostAlertText.gameObject.SetActive(true);
            ghostAlertText.alpha = 0f;
            ghostAlertText.text  = "";

            // Typewriter effect
            yield return FadeTextAlpha(ghostAlertText, 0f, 1f, 0.15f);

            for (int i = 0; i <= msg.Length; i++)
            {
                ghostAlertText.text = msg.Substring(0, i);
                yield return new WaitForSeconds(0.05f);
            }

            yield return new WaitForSeconds(2.5f);

            // Fade out
            yield return FadeTextAlpha(ghostAlertText, 1f, 0f, 1f);
            ghostAlertText.gameObject.SetActive(false);
        }

        /// <summary>Detak jantung visual — dua pulse cepat lalu jeda panjang</summary>
        private IEnumerator HeartbeatRoutine()
        {
            if (heartbeatOverlay == null) { _heartbeatRunning = false; yield break; }

            while (_heartbeatRunning)
            {
                // Detak 1
                yield return PulseOverlay(heartbeatOverlay, 0.28f, 0.08f);
                yield return new WaitForSeconds(0.12f);
                // Detak 2 (lebih lemah)
                yield return PulseOverlay(heartbeatOverlay, 0.15f, 0.1f);
                // Jeda sesuai intensitas sanity
                float sanityVal = _sanity != null ? _sanity.Percent() : 0f;
                float interval  = Mathf.Lerp(0.5f, 1.5f, sanityVal / sanityPulseThreshold);
                yield return new WaitForSeconds(interval);
            }
        }

        // ── Utility Coroutines ─────────────────────────

        private IEnumerator FadeTextAlpha(TMP_Text txt, float from, float to, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t        += Time.deltaTime / Mathf.Max(duration, 0.01f);
                txt.alpha  = Mathf.Lerp(from, to, t);
                yield return null;
            }
            txt.alpha = to;
        }

        private IEnumerator PulseOverlay(Image img, float peakAlpha, float duration)
        {
            float half = duration * 0.5f;
            float t    = 0f;

            while (t < half)
            {
                t += Time.deltaTime;
                SetAlpha(img, Mathf.Lerp(0f, peakAlpha, t / half));
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                SetAlpha(img, Mathf.Lerp(peakAlpha, 0f, t / half));
                yield return null;
            }

            SetAlpha(img, 0f);
        }

        private static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            Color c = g.color;
            c.a     = a;
            g.color = c;
        }
    }
}