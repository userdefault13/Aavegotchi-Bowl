using UnityEngine;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Retro Bowl–style movement: digital input, hard stop, no physics coast.
    /// Keyboard WASD/arrows + gamepad D-pad. Right stick is reserved for throwing.
    /// </summary>
    public static class ArcadeMove
    {
        /// <summary>WASD / arrows / gamepad D-pad (no sticks — right stick aims).</summary>
        public static Vector2 ReadDigitalPlanar()
        {
            float x = 0f;
            float y = 0f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;

            if (x == 0f && y == 0f)
            {
                Vector2 dpad = TecmoInput.ReadGamepadDpadDigital();
                x = dpad.x;
                y = dpad.y;
            }

            return new Vector2(x, y);
        }

        public static void ConfigureKinematicBody(Rigidbody rb)
        {
            if (rb == null) return;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Apply planar velocity with MovePosition (kinematic) — stops instantly when vel is 0.</summary>
        public static void Apply(Rigidbody rb, Vector3 velocity)
        {
            if (rb == null) return;
            velocity.z = 0f;

            if (!rb.isKinematic)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }

            float dt = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;

            if (velocity.sqrMagnitude < 0.0001f)
            {
                if (Time.inFixedTimeStep)
                    rb.MovePosition(rb.position);
                return;
            }

            Vector3 next = rb.position + velocity * dt;
            next.z = 0f;
            // Update() AI: write transform directly — MovePosition from Update is unreliable.
            if (Time.inFixedTimeStep)
                rb.MovePosition(next);
            else
            {
                rb.transform.position = next;
                rb.position = next;
            }
        }
    }
}
