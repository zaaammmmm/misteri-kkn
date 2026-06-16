using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace KKN.Game.Systems
{
    public class LoadingController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text percentageText;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private TMP_Text statusText;

        [Header("Timing")]
        [SerializeField] private float minimumLoadingTime = 3f;
        [SerializeField] private float progressSmoothSpeed = 0.5f;
        
        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip loadingMusic;
        [SerializeField] private bool loopMusic = true;

        private float displayedProgress = 0f;
        private bool isFinishing = false;

        private readonly string[] tips =
        {
            "Jangan terlalu lama berada di luar rumah.",
            "Suara keras dapat menarik perhatian makhluk tertentu.",
            "Cari material sebelum malam semakin larut.",
            "Generator adalah harapan terakhirmu.",
            "Tidak semua suara berasal dari manusia."
        };

        private void Start()
        {
            if (tipText != null)
            {
                tipText.text =
                    tips[Random.Range(0, tips.Length)];
            }

            if (progressBar != null)
                progressBar.value = 0f;

            if (percentageText != null)
                percentageText.text = "0%";

            PlayLoadingMusic();

            StartCoroutine(LoadRoutine());
        }

        private void PlayLoadingMusic()
        {
            if (musicSource == null || loadingMusic == null)
                return;

            musicSource.clip = loadingMusic;
            musicSource.loop = loopMusic;
            musicSource.Play();
        }

        private IEnumerator LoadRoutine()
        {
            if (string.IsNullOrEmpty(LoadingData.TargetScene))
            {
                Debug.LogError(
                    "[Loading] TargetScene kosong!"
                );

                yield break;
            }

            Debug.Log(
                "[Loading] Target = " +
                LoadingData.TargetScene
            );

            yield return null;

            AsyncOperation operation =
                SceneManager.LoadSceneAsync(
                    LoadingData.TargetScene
                );

            if (operation == null)
            {
                Debug.LogError(
                    "[Loading] Gagal memuat scene: " +
                    LoadingData.TargetScene
                );

                yield break;
            }

            operation.allowSceneActivation = false;

            float timer = 0f;

            while (!operation.isDone)
            {
                timer += Time.deltaTime;

                float targetProgress;

                if (operation.progress < 0.9f)
                {
                    // 0% - 90%
                    targetProgress =
                        (operation.progress / 0.9f) * 0.9f;
                }
                else
                {
                    // 90% - 100%
                    float t =
                        Mathf.Clamp01(
                            timer / minimumLoadingTime
                        );

                    targetProgress =
                        Mathf.Lerp(
                            0.9f,
                            1f,
                            t
                        );
                }

                displayedProgress =
                    Mathf.MoveTowards(
                        displayedProgress,
                        targetProgress,
                        progressSmoothSpeed * Time.deltaTime
                    );

                if (progressBar != null)
                    progressBar.value = displayedProgress;

                if (percentageText != null)
                {
                    percentageText.text =
                        Mathf.RoundToInt(
                            displayedProgress * 100f
                        ) + "%";
                }

                UpdateLoadingStatus(
                    displayedProgress
                );

                if (
                    !isFinishing &&
                    operation.progress >= 0.9f &&
                    timer >= minimumLoadingTime &&
                    displayedProgress >= 0.99f
                )
                {
                    isFinishing = true;

                    yield return new WaitForSeconds(0.3f);

                    if (KKN_FadeController.Instance != null)
                    {
                        yield return StartCoroutine(
                            KKN_FadeController.Instance
                                .FadeOut(0.5f)
                        );
                    }

                    if (musicSource != null)
                        yield return StartCoroutine(FadeOutMusic(1f));

                    operation.allowSceneActivation = true;
                }

                yield return null;
            }
        }

       private IEnumerator FadeOutMusic(float duration)
        {
            if (musicSource == null)
                yield break;

            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                musicSource.volume =
                    Mathf.Lerp(
                        startVolume,
                        0f,
                        Mathf.Clamp01(elapsed / duration)
                    );

                yield return null;
            }

            musicSource.volume = 0f;
            musicSource.Stop();
            musicSource.volume = startVolume;
        }

        private void UpdateLoadingStatus(
            float progress)
        {
            if (statusText == null)
                return;

            if (progress < 0.25f)
            {
                statusText.text =
                    "Memuat Lingkungan...";
            }
            else if (progress < 0.50f)
            {
                statusText.text =
                    "Memuat Objek Desa...";
            }
            else if (progress < 0.75f)
            {
                statusText.text =
                    "Memuat AI Hantu...";
            }
            else if (progress < 0.95f)
            {
                statusText.text =
                    "Menyiapkan Kegelapan...";
            }
            else
            {
                statusText.text =
                    "Hampir Selesai...";
            }
        }
    }
}