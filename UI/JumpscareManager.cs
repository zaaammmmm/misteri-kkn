using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using KKN.Game.Core;
using KKN.Game.Player;

namespace KKN.Game.UI
{
    /// <summary>
    /// Manages jumpscare sequences and game over transitions.
    /// Uses PlayerState for safe player freezing.
    /// </summary>
    public class JumpscareManager : MonoBehaviour
    {
        public static JumpscareManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private Image bloodFlash;
        [SerializeField] private Image blackFade;
        [SerializeField] private GameObject gameOverPanel;

        [Header("Audio")]
        [SerializeField] private AudioSource screamAudio;
        [SerializeField] private AudioClip sanityDeathClip;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void StartJumpscare(Transform ghost)
        {
            StartCoroutine(JumpscareSequence(ghost));
        }

        IEnumerator JumpscareSequence(Transform ghost)
        {
            PlayerFreeze(true);
            GameManager.Instance?.StartCutscene();

            Camera cam = Camera.main;

            if (screamAudio != null)
                screamAudio.Play();

            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;

                if (cam != null && ghost != null)
                {
                    Vector3 dir = (ghost.position - cam.transform.position).normalized;
                    Quaternion lookRot = Quaternion.LookRotation(dir);
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, lookRot, 8f * Time.deltaTime);

                    ghost.position = Vector3.Lerp(
                        ghost.position,
                        cam.transform.position + cam.transform.forward * 0.7f,
                        5f * Time.deltaTime);
                }

                SetAlpha(bloodFlash, Mathf.PingPong(t * 2f, 0.7f));
                yield return null;
            }

            yield return StartCoroutine(FadeBlack());
            ShowGameOver();
        }

        public void TriggerSanityDeath()
        {
            StartCoroutine(SanityDeathSequence());
        }

        IEnumerator SanityDeathSequence()
        {
            PlayerFreeze(true);
            GameManager.Instance?.GameOver();

            if (sanityDeathClip != null && screamAudio != null)
                screamAudio.PlayOneShot(sanityDeathClip);
            else if (screamAudio != null)
                screamAudio.Play();

            SetAlpha(bloodFlash, 0.5f);
            yield return new WaitForSeconds(0.3f);
            SetAlpha(bloodFlash, 0f);

            yield return StartCoroutine(FadeBlack());
            ShowGameOver();
        }

        IEnumerator FadeBlack()
        {
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 1.5f;
                SetAlpha(blackFade, Mathf.Clamp01(t));
                yield return null;
            }
        }

        void ShowGameOver()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = a;
            img.color = c;
        }

        void PlayerFreeze(bool freeze)
        {
            if (PlayerState.Instance != null)
                PlayerState.Instance.IsFrozen = freeze;

            var player = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = !freeze;
            }
        }
    }
}

