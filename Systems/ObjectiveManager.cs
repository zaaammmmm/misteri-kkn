using UnityEngine;
using TMPro;
using System.Collections;
using KKN.Game.Data;
using KKN.Game.Core;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Manages objective progression with support for varied objective types.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        [Header("Popup UI")]
        [SerializeField] private TMP_Text objectiveText;

        [Header("Objectives")]
        [SerializeField] private ObjectiveData[] objectives;

        public int currentStep { get; private set; } = 0;

        private string currentObjective = "";
        private Coroutine hideRoutine;
        private bool isGlitching = false;

        public event System.Action<int> OnObjectiveChanged;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            SetObjective();
            ShowTemporary();
        }

        public void NextStep()
        {
            currentStep++;
            SetObjective();
            ShowTemporary();
            OnObjectiveChanged?.Invoke(currentStep);
        }

        void SetObjective()
        {
            if (objectives != null && currentStep < objectives.Length)
            {
                currentObjective = objectives[currentStep].description;
            }
            else
            {
                currentObjective = GetLegacyObjective();
            }

            if (objectiveText != null)
                objectiveText.text = currentObjective;
        }

        string GetLegacyObjective()
        {
            return currentStep switch
            {
                0 => "Cari jalan keluar kamar...",
                1 => "Temukan IntroKey",
                2 => "Buka pintu kamar",
                3 => "Cari MainKey",
                4 => "Cari ExitKey",
                5 => "Keluar dari kontrakan",
                6 => "Cari GeneratorKey",
                7 => "Nyalakan Generator",
                8 => "Listrik Menyala...",
                _ => ""
            };
        }

        public void GlitchObjectiveText()
        {
            if (isGlitching || objectiveText == null) return;
            StartCoroutine(GlitchRoutine());
        }

        IEnumerator GlitchRoutine()
        {
            isGlitching = true;
            string original = objectiveText.text;
            string chars = "!@#$%^&*()_+-=[]{}|;':,./<>?~`";

            for (int i = 0; i < 8; i++)
            {
                string glitched = "";
                for (int j = 0; j < original.Length; j++)
                {
                    glitched += Random.value < 0.3f ? chars[Random.Range(0, chars.Length)] : original[j];
                }
                objectiveText.text = glitched;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            }

            objectiveText.text = original;
            isGlitching = false;
        }

        void ShowTemporary()
        {
            if (objectiveText == null) return;

            objectiveText.gameObject.SetActive(true);

            if (hideRoutine != null)
                StopCoroutine(hideRoutine);

            hideRoutine = StartCoroutine(HideAfterSeconds());
        }

        IEnumerator HideAfterSeconds()
        {
            yield return new WaitForSeconds(GameConstants.OBJECTIVE_SHOW_TIME);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * GameConstants.UI_FADE_SPEED;
                if (objectiveText != null)
                    objectiveText.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            if (objectiveText != null)
                objectiveText.gameObject.SetActive(false);
        }

        public string GetCurrentObjective() => currentObjective;
    }
}

