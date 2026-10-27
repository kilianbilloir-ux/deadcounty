using UnityEngine;

namespace DeadCounty.Audio
{
    // Freezes world audio while time is stopped (pause menu, title screen); sources flagged ignoreListenerPause
    // (Interface and Music cues) keep playing.
    sealed class SfxPauseWatcher : MonoBehaviour
    {
        void Update()
        {
            var paused = Time.timeScale == 0f;
            if (AudioListener.pause != paused) AudioListener.pause = paused;
        }

        void OnDestroy() => AudioListener.pause = false;
    }
}
