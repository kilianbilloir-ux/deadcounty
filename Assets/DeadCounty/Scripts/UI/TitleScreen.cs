using System;
using DeadCounty.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace DeadCounty.UI
{
    // Title screen over the game world (GDD 8.11, after Risk of Rain 2): on load the player's menu shows Jouer,
    // Options and Quitter with the game clock stopped, while this camera sways slowly over the town. Jouer hands
    // the view back to the player camera through the Cinemachine blend.
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField] CinemachineCamera titleCamera;
        [Tooltip("Half the sway of the camera heading (deg).")]
        [SerializeField] float swayAngle = 4f;
        [Tooltip("Seconds for a full sway, in real time.")]
        [SerializeField] float swayPeriod = 30f;

        GameMenu menu;
        Quaternion rest;

        void Awake()
        {
            if (titleCamera == null) throw new InvalidOperationException($"TitleScreen '{name}' has no title camera");
            menu = FindAnyObjectByType<GameMenu>();
            if (menu == null) throw new InvalidOperationException($"TitleScreen '{name}' found no GameMenu in the scene");
            rest = titleCamera.transform.rotation;
        }

        void OnEnable() => menu.Started += OnStarted;

        void OnDisable() => menu.Started -= OnStarted;

        void Start()
        {
            titleCamera.enabled = true;
            menu.ShowTitle();
        }

        // Real time: the game clock is stopped behind the title.
        void Update()
        {
            if (!titleCamera.enabled) return;
            var yaw = swayAngle * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / swayPeriod);
            titleCamera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * rest;
        }

        void OnStarted() => titleCamera.enabled = false;
    }
}
