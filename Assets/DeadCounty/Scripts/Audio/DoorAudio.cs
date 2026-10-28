using DeadCounty.World;
using UnityEngine;

namespace DeadCounty.Audio
{
    // Sounds of a door's own swing, read from its angle so the door needs no hook: the latch and a hinge creak when
    // it starts to open, a creak when it starts to close, the thud (or slam) when it shuts, the frame splintering
    // when zombies force it and the leaf banging into its stop after a violent swing. Swing speeds tell a calm use
    // from a slam (player in a hurry) or a forced door; the zombies' knocking while they push is ZombieAudio's.
    [RequireComponent(typeof(Door))]
    public class DoorAudio : MonoBehaviour
    {
        const float SoundHeight = 1f;
        // Default swing speeds: 190 deg/s calm, 317 forced, 633 slammed.
        const float ForcedSpeed = 250f;
        const float SlamSpeed = 450f;
        const float ShutAngle = 0.5f;

        [SerializeField] SoundCue latch;
        [SerializeField] SoundCue creak;
        [SerializeField] SoundCue shut;
        [SerializeField] SoundCue slam;
        [SerializeField] SoundCue burst;

        Door door;
        float lastAngle, lastDelta, swingSpeed;

        void Awake() => door = GetComponent<Door>();

        void Start() => lastAngle = door.Angle;

        void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            var angle = door.Angle;
            var delta = angle - lastAngle;
            var moving = delta != 0f;
            var wasMoving = lastDelta != 0f;
            if (moving && (!wasMoving || Mathf.Sign(delta) != Mathf.Sign(lastDelta)))
                Started(lastAngle, angle, Mathf.Abs(delta) / dt);
            else if (!moving && wasMoving)
                Stopped(angle);
            lastAngle = angle;
            lastDelta = delta;
        }

        void Started(float from, float to, float speed)
        {
            swingSpeed = speed;
            var at = Position;
            var opening = Mathf.Abs(to) > Mathf.Abs(from);
            if (opening)
            {
                if (Mathf.Abs(from) < ShutAngle)
                {
                    if (speed >= ForcedSpeed && speed < SlamSpeed) Sfx.Play(burst, at);
                    else Sfx.Play(latch, at);
                }
                if (speed < ForcedSpeed) Sfx.Play(creak, at);
                else if (speed >= SlamSpeed) Sfx.Play(creak, at, 0.5f, 1.3f);
            }
            else if (speed < SlamSpeed)
            {
                Sfx.Play(creak, at, 0.8f);
            }
        }

        void Stopped(float angle)
        {
            var at = Position;
            if (Mathf.Abs(angle) < ShutAngle) Sfx.Play(swingSpeed >= SlamSpeed ? slam : shut, at);
            else if (swingSpeed >= ForcedSpeed) Sfx.Play(slam, at, 0.5f, 1.1f);
        }

        Vector3 Position => door.Doorway.Center + Vector3.up * SoundHeight;
    }
}
