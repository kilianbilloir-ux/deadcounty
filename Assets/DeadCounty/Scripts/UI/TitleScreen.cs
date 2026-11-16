using System;
using DeadCounty.Core;
using DeadCounty.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace DeadCounty.UI
{
    // GDD 8.11, after Risk of Rain 2: the player's GameMenu draws the title while this camera sways over the town
    // at first light; Jouer hands the view back through the Cinemachine blend while the sun climbs to the hour the
    // game starts at.
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField] CinemachineCamera titleCamera;
        [Tooltip("Half the sway of the camera heading (deg).")]
        [SerializeField] float swayAngle = 4f;
        [Tooltip("Seconds for a full sway, in real time.")]
        [SerializeField] float swayPeriod = 30f;
        [Tooltip("Hour of the sky behind the title: first light, with the street lamps still on.")]
        [SerializeField] float titleHour = 6.2f;
        [Tooltip("Seconds the sky takes to reach the game's hour once Jouer is chosen.")]
        [SerializeField] float sunriseSeconds = 3f;

        GameMenu menu;
        DayNightDirector clock;
        Quaternion rest;
        float gameHour;
        // Seconds into the sunrise, negative when none is under way.
        float sunrise = -1f;

        public float TitleHour => titleHour;
        public float SunriseSeconds => sunriseSeconds;

        void Awake()
        {
            if (titleCamera == null) throw new InvalidOperationException($"TitleScreen '{name}' has no title camera");
            menu = FindAnyObjectByType<GameMenu>();
            if (menu == null) throw new InvalidOperationException($"TitleScreen '{name}' found no GameMenu in the scene");
            rest = titleCamera.transform.rotation;
        }

        void OnEnable() => menu.Started += OnStarted;

        void OnDisable() => menu.Started -= OnStarted;

        // Not in Awake: the clock sets its own hour there. A scene without one keeps its sky.
        void Start()
        {
            titleCamera.enabled = true;
            clock = FindAnyObjectByType<DayNightDirector>();
            if (clock != null)
            {
                gameHour = clock.Hour;
                clock.SetHour(titleHour);
            }
            menu.ShowTitle();
        }

        void Update()
        {
            if (sunrise >= 0f) Rise();
            if (!titleCamera.enabled) return;
            // Real time: the game clock is stopped behind the title.
            var yaw = swayAngle * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / swayPeriod);
            titleCamera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * rest;
        }

        // Game time, which runs again: the clock ends on the hour it would have started at.
        void Rise()
        {
            sunrise += Time.deltaTime;
            var progress = Mathf.Clamp01(sunrise / sunriseSeconds);
            clock.SetHour(Mathf.Lerp(titleHour, gameHour, Mathf.SmoothStep(0f, 1f, progress)));
            if (progress >= 1f) sunrise = -1f;
        }

        void OnStarted()
        {
            titleCamera.enabled = false;
            if (clock != null) sunrise = 0f;
        }
    }
}
