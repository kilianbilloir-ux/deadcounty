using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Development overlay toggled with F1 (hidden by default): player state, health, stamina, noise radius and
    // camera framing values. While it is open, K deals a 25 damage hit from in front of the player to test the hit
    // reaction and death flow.
    // The noise radius is also drawn as a gizmo (Scene view, or Game view with Gizmos enabled).
    public class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] bool visible;

        ThirdPersonController player;
        PlayerHealth health;
        PlayerStamina stamina;
        PlayerCameraDirector director;
        CinemachineThirdPersonFollow follow;
        GUIStyle style;
        float fps;

        void Awake()
        {
            player = GetComponentInChildren<ThirdPersonController>();
            health = player.GetComponent<PlayerHealth>();
            stamina = player.GetComponent<PlayerStamina>();
            director = GetComponentInChildren<PlayerCameraDirector>();
            follow = director.GetComponent<CinemachineThirdPersonFollow>();
        }

        void Update()
        {
            // Direct device reads: debug keys do not belong in the gameplay action map.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) visible = !visible;
            if (visible && keyboard != null && keyboard.kKey.wasPressedThisFrame)
                health.ApplyHit(25f, player.transform.position + player.transform.forward);
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.1f);
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true, padding = new RectOffset(10, 10, 8, 8) };
            var state = !player.IsGrounded ? "en l'air"
                : player.IsSliding ? "glisse"
                : player.IsCrouching ? "accroupi"
                : player.CurrentSpeed < 0.1f ? "immobile"
                : player.Gait switch { Gait.Sprint => "sprint", Gait.Run => "course", _ => "marche" };
            var text = "<b>Debug joueur</b>  (F1, K : coup)\n"
                + $"{fps:0} fps\n"
                + $"État : {state}\n"
                + $"Vitesse : {player.CurrentSpeed:0.0} m/s\n"
                + $"Santé : {health.Pool.Current:0}/{health.Pool.Max:0}\n"
                + $"Endurance : {stamina.Model.Current:0}{(stamina.Model.Exhausted ? " (épuisé)" : "")}\n"
                + $"Pente : {player.GroundAngle:0}°\n"
                + $"Bruit : {player.NoiseRadius:0.0} m\n"
                + $"Caméra : {follow.CameraDistance:0.0} m, épaule {(follow.CameraSide > 0.5f ? "droite" : "gauche")}\n"
                + $"Confinement : {director.Confinement:0.00}";
            GUI.Box(new Rect(10f, 10f, 280f, 196f), text, style);
        }

        void OnDrawGizmos()
        {
            if (player == null || player.NoiseRadius <= 0f) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(player.transform.position, player.NoiseRadius);
        }
    }
}
