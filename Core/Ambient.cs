using UnityEngine;

namespace KKN.Game.Systems
{
    public class GameplayAmbient : MonoBehaviour
    {
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioClip villageAmbient;

        private void Start()
        {
            ambientSource.clip = villageAmbient;
            ambientSource.loop = true;
            ambientSource.Play();
        }
    }
}