using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Development overlay toggled with F1: player state, noise radius and camera framing values.
    // The noise radius is also drawn as a gizmo (Scene view, or Game view with Gizmos enabled).
    public class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] bool visible = true;

        ThirdPersonController player;
        PlayerCameraDirector director;
        CinemachineThirdPersonFollow follow;
        GUIStyle style;
        float fps;

        void Awake()
        {
            player = GetComponentInChildren<ThirdPersonController>();
            director = GetComponentInChildren<PlayerCameraDirector>();
            follow = director.GetComponent<CinemachineThirdPersonFollow>();
        }

        void Update()
        {
            // Direct device read: a debug toggle does not belong in the gameplay action map.
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) visible = !visible;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.1f);
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true, padding = new RectOffset(10, 10, 8, 8) };
            var state = !player.IsGrounded ? "en l'air"
                : player.IsSliding ? "glisse"
                : player.IsCrouching ? "accroupi"
                : player.IsSprinting ? "sprint"
                : player.CurrentSpeed > 0.1f ? "marche" : "immobile";
            var text = "<b>Debug joueur</b>  (F1)\n"
                + $"{fps:0} fps\n"
                + $"État : {state}\n"
                + $"Vitesse : {player.CurrentSpeed:0.0} m/s\n"
                + $"Pente : {player.GroundAngle:0}°\n"
                + $"Bruit : {player.NoiseRadius:0.0} m\n"
                + $"Caméra : {follow.CameraDistance:0.0} m, épaule {(follow.CameraSide > 0.5f ? "droite" : "gauche")}\n"
                + $"Confinement : {director.Confinement:0.00}";
            GUI.Box(new Rect(10f, 10f, 280f, 160f), text, style);
        }

        void OnDrawGizmos()
        {
            if (player == null || player.NoiseRadius <= 0f) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(player.transform.position, player.NoiseRadius);
        }
    }
}
