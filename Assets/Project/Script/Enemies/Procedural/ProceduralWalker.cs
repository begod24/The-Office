using System;
using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Enemies
{
    [DisallowMultipleComponent]
    public sealed class ProceduralWalker : MonoBehaviour
    {
        [Tooltip("The bone every leg hangs from. It is the only part above the legs this " +
                 "component moves — everything mounted on it rides along.")]
        [SerializeField] private Transform body;

        [SerializeField] private ProceduralLeg[] legs = Array.Empty<ProceduralLeg>();

        [Header("Stride")]
        [Tooltip("Metres a foot may sit away from its home stance before the leg has to step. " +
                 "This is the leg's spare reach rather than the stride: a foot asked to travel " +
                 "further than this can only be reached with the knee locked straight.")]
        [Min(0.02f)]
        [SerializeField] private float stepReach = 0.4f;

        [Min(0.01f)]
        [SerializeField] private float stepHeight = 0.18f;

        [Tooltip("Seconds a swing takes when running and when standing still. The swing is sized " +
                 "from the speed in between, so walking faster means shorter steps taken more " +
                 "often — the one thing that separates walking from skating.")]
        [Min(0.02f)]
        [SerializeField] private float minStepDuration = 0.14f;

        [Min(0.05f)]
        [SerializeField] private float maxStepDuration = 0.45f;

        [Tooltip("Fraction of Step Reach a foot may drift while the body is still before it " +
                 "shuffles back into stance. Too small and the thing twitches while turning on " +
                 "the spot; too large and it stands in a pose it walked out of.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float settleFraction = 0.3f;

        [Tooltip("Degrees a hip bone yaws toward the foot it carries.")]
        [Range(0f, 90f)]
        [SerializeField] private float hipYaw = 30f;

        [Header("Body")]
        [Tooltip("How quickly the body settles onto the height its feet report. Low values read " +
                 "as a heavy thing, high values as a nervous one.")]
        [Min(0.5f)]
        [SerializeField] private float bodyFollowSpeed = 8f;

        [Tooltip("Degrees the body may pitch and roll to match the ground its feet found. This " +
                 "is what makes a step or a ramp visible in the silhouette.")]
        [Range(0f, 45f)]
        [SerializeField] private float maxTilt = 12f;

        [Tooltip("Metres the body lifts while a leg is swinging. Small: this is a gait tell, not " +
                 "a bounce.")]
        [Min(0f)]
        [SerializeField] private float bobAmplitude = 0.03f;

        [Min(0f)]
        [SerializeField] private float breatheAmplitude = 0.015f;

        [Min(0.01f)]
        [SerializeField] private float breatheFrequency = 0.4f;

        [Tooltip("Degrees of forward lean per metre per second of speed, and per metre per " +
                 "second squared of sideways acceleration. Leaning into its own motion is most " +
                 "of what makes a walk look driven rather than dragged.")]
        [Min(0f)]
        [SerializeField] private float leanPerSpeed = 1.5f;

        [Min(0f)]
        [SerializeField] private float leanPerAcceleration = 0.6f;

        [Range(0f, 45f)]
        [SerializeField] private float maxLean = 10f;

        [Header("Death")]
        [Tooltip("Metres the body sinks, and degrees it sags forward, at full collapse. The feet " +
                 "stay where they were planted, so the legs fold under it.")]
        [Min(0f)]
        [SerializeField] private float collapseDrop = 0.45f;

        [Range(0f, 90f)]
        [SerializeField] private float collapseTilt = 25f;

        [Header("Ground")]
        [Tooltip("Metres above and below the body's own height a foot looks for floor.")]
        [Min(0.1f)]
        [SerializeField] private float probeUp = 1.2f;

        [Min(0.1f)]
        [SerializeField] private float probeDown = 3f;

        [Tooltip("Metres of movement in one frame that means the body was teleported rather than " +
                 "walked — a spawn, a pool reuse, a navmesh snap. The feet are replanted instead " +
                 "of being dragged across the room.")]
        [Min(0.5f)]
        [SerializeField] private float teleportDistance = 3f;

        [Tooltip("Metres from the player before this walker stops looking for floor and stands " +
                 "its feet on the plane its body is on. A foot a few centimetres off the ground " +
                 "is not readable at that range, and probing is the most expensive thing a swarm " +
                 "asks of the machine watching it.")]
        [Min(0f)]
        [SerializeField] private float probeDistance = 30f;

        private sealed class LegState
        {
            public Vector3 HomeLocal;
            public Vector3 Planted;
            public Vector3 Current;

            public bool Swinging;
            public float SwingTime;
            public float SwingDuration;
            public Vector3 SwingOrigin;
            public float Arc;
            public float Yaw;

            public Quaternion RestHip;
            public Quaternion RestThigh;
            public Quaternion RestShin;
            public Quaternion RestTarsus;
            public Quaternion RestFoot;

            public Vector3 PoleLocal;
            public Transform PoleSpace;

            public Vector3 RestDirectionInBody;
            public Vector3 TarsusOffsetLocal;

            public float ThighLength;
            public float ShinLength;
        }

        // One frame's answer for one foot. Resolved says the batch was asked about this leg at
        // all, which is what separates "there is no floor here" from "nobody looked".
        private struct Probe
        {
            public bool Resolved;
            public bool Hit;
            public float Height;
        }

        private LegState[] states = Array.Empty<LegState>();
        private Probe[] probes = Array.Empty<Probe>();

        private Vector3 restBodyPosition;
        private Quaternion restBodyRotation;

        private Vector3 previousPosition;
        private Vector3 velocity;
        private Vector3 acceleration;
        private bool hasPrevious;

        private float groundHeight;
        private float breathePhase;

        private bool ready;

        // The stride this frame was prepared with. Kept rather than recomputed so the tick lands
        // on exactly the stride the probes were aimed for — a probe taken for one lead and used
        // for another is a foot placed on floor that was never under it.
        private Vector3 pendingLead;
        private float pendingDuration;

        private int preparedFrame = -1;
        private bool probesSkipped;

        // Set by the view that owns this walker: a recoil shove, a windup crouch, the sag of a
        // dead thing. Kept as plain properties because they are per-frame intent, not authored
        // data — serialising them would invite someone to tune a recoil in the prefab.
        public Vector3 BodyOffset { get; set; }

        public float ExtraPitch { get; set; }

        public float ExtraRoll { get; set; }

        public float Collapse { get; set; }

        public Vector3 Velocity => velocity;

        public float Speed => velocity.magnitude;

        public Vector3 Acceleration => acceleration;

        public Transform Body => body;

        private void Awake()
        {
            breathePhase = UnityEngine.Random.value * Mathf.PI * 2f;
            Rebuild();
        }

        // Reads the rig's rest pose: every bone's local rotation, each foot's home stance from
        // its IK bone, each knee's pole, and the segment lengths. Public so an editor preview can
        // step the gait without entering play mode — Awake does not run in edit mode, and a gait
        // nobody can watch is a gait nobody tunes.
        public void Rebuild() => CacheRestPose();

        private void OnEnable()
        {
            hasPrevious = false;
            velocity = Vector3.zero;
            acceleration = Vector3.zero;
            groundHeight = 0f;
            preparedFrame = -1;

            GroundProbes.Register(this);
        }

        private void OnDisable() => GroundProbes.Unregister(this);

        private void LateUpdate() => Tick(Time.deltaTime);

        // Public so the same code can be stepped by hand from an editor preview: an animation
        // nobody can look at without entering play mode is an animation nobody tunes.
        public void Tick(float deltaTime)
        {
            if (!ready || deltaTime <= 0f) return;

            // Normally GroundProbes prepared this frame and filled the feet in before this ran.
            // An editor preview has no driver, so it prepares its own and every foot falls back
            // to its own cast — the right trade for one walker nobody is playing against.
            if (preparedFrame != Time.frameCount) Prepare(deltaTime);

            ResetPose();
            UpdateSteps(deltaTime);
            UpdateBody(deltaTime);
            SolveLegs();
        }

        // Called by GroundProbes before this frame's tick. The motion estimate has to be current
        // for the probes to be aimed where the feet are actually going, so it is taken here rather
        // than inside the tick.
        internal void CollectProbes(float deltaTime, Vector3 viewer, bool hasViewer,
            List<GroundProbes.ProbeRequest> into)
        {
            if (!ready || deltaTime <= 0f) return;

            Prepare(deltaTime);

            // A collapsing thing has stopped stepping, and a walker too far away to read does not
            // earn a cast per foot. Both stand on the plane their body is on instead.
            if (Collapse > 0.5f ||
                (hasViewer && (transform.position - viewer).sqrMagnitude >
                    probeDistance * probeDistance))
            {
                probesSkipped = true;
                return;
            }

            var height = transform.position.y + probeUp;
            var distance = probeUp + probeDown;

            for (var i = 0; i < states.Length; i++)
            {
                if (!states[i].Swinging) continue;

                var desired = transform.TransformPoint(states[i].HomeLocal) + pendingLead;

                into.Add(new GroundProbes.ProbeRequest(this, i,
                    new Vector3(desired.x, height, desired.z), distance));
            }
        }

        internal void ApplyProbe(int leg, bool hit, float height)
        {
            if (leg < 0 || leg >= probes.Length) return;

            probes[leg] = new Probe { Resolved = true, Hit = hit, Height = height };
        }

        private void Prepare(float deltaTime)
        {
            preparedFrame = Time.frameCount;
            probesSkipped = false;

            for (var i = 0; i < probes.Length; i++) probes[i] = default;

            UpdateMotion(deltaTime);

            var speed = velocity.magnitude;

            // One swing carries the foot the whole of its spare reach, so a faster body gets a
            // shorter swing. The clamps are what keeps a sprint from turning into a blur and a
            // crawl from freezing mid-step.
            pendingDuration = speed > 0.01f
                ? Mathf.Clamp(stepReach / speed, minStepDuration, maxStepDuration)
                : maxStepDuration;

            // The foot is aimed one swing ahead of where the body is now, so it lands where the
            // body will be rather than where it was. Without this every step lands behind and the
            // legs trail the body like a dragged chair.
            pendingLead = Vector3.ClampMagnitude(velocity * pendingDuration, stepReach);
        }

        private void CacheRestPose()
        {
            if (body == null)
            {
                Debug.LogError($"[Enemy] {name} has no body bone assigned — nothing to walk on.",
                    this);
                return;
            }

            restBodyPosition = body.localPosition;
            restBodyRotation = body.localRotation;

            states = new LegState[legs.Length];
            probes = new Probe[legs.Length];

            for (var i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];

                if (leg == null || leg.Thigh == null || leg.Shin == null || leg.Foot == null ||
                    leg.Target == null)
                {
                    Debug.LogError($"[Enemy] {name} leg {i} is missing a bone. The walker is " +
                                   "disabled rather than solving half a skeleton.", this);
                    enabled = false;
                    return;
                }

                var tip = leg.Tarsus != null ? leg.Tarsus : leg.Foot;
                var poleSpace = leg.Hip != null ? leg.Hip : body;

                var state = new LegState
                {
                    HomeLocal = transform.InverseTransformPoint(leg.Target.position),
                    RestHip = leg.Hip != null ? leg.Hip.localRotation : Quaternion.identity,
                    RestThigh = leg.Thigh.localRotation,
                    RestShin = leg.Shin.localRotation,
                    RestTarsus = leg.Tarsus != null ? leg.Tarsus.localRotation : Quaternion.identity,
                    RestFoot = Quaternion.Inverse(transform.rotation) * leg.Foot.rotation,
                    PoleSpace = poleSpace,
                    PoleLocal = leg.Pole != null
                        ? poleSpace.InverseTransformPoint(leg.Pole.position)
                        : poleSpace.InverseTransformPoint(leg.Shin.position + Vector3.up),
                    ThighLength = Vector3.Distance(leg.Thigh.position, leg.Shin.position),
                    ShinLength = Vector3.Distance(leg.Shin.position, tip.position),
                    TarsusOffsetLocal = leg.Tarsus != null
                        ? transform.InverseTransformVector(leg.Tarsus.position - leg.Foot.position)
                        : Vector3.zero
                };

                var hipPosition = leg.Hip != null ? leg.Hip.position : leg.Thigh.position;
                state.RestDirectionInBody =
                    body.InverseTransformVector(leg.Target.position - hipPosition);

                state.Planted = leg.Target.position;
                state.Current = state.Planted;

                states[i] = state;
            }

            ready = states.Length > 0;
        }

        private void UpdateMotion(float deltaTime)
        {
            var position = transform.position;

            if (!hasPrevious)
            {
                previousPosition = position;
                hasPrevious = true;
                ReplantFeet();
                return;
            }

            var delta = position - previousPosition;
            previousPosition = position;

            if (delta.sqrMagnitude > teleportDistance * teleportDistance)
            {
                velocity = Vector3.zero;
                acceleration = Vector3.zero;
                groundHeight = 0f;
                ReplantFeet();
                return;
            }

            var instant = delta / deltaTime;
            instant.y = 0f;

            var blend = 1f - Mathf.Exp(-10f * deltaTime);
            var previousVelocity = velocity;

            velocity = Vector3.Lerp(velocity, instant, blend);
            acceleration = Vector3.Lerp(acceleration, (velocity - previousVelocity) / deltaTime,
                blend);
        }

        private void ReplantFeet()
        {
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];

                state.Swinging = false;
                state.Planted = GroundDirect(i, Vector3.zero);
                state.Current = state.Planted;
            }
        }

        private void ResetPose()
        {
            body.localPosition = restBodyPosition;
            body.localRotation = restBodyRotation;

            for (var i = 0; i < states.Length; i++)
            {
                var leg = legs[i];
                var state = states[i];

                if (leg.Hip != null) leg.Hip.localRotation = state.RestHip;

                leg.Thigh.localRotation = state.RestThigh;
                leg.Shin.localRotation = state.RestShin;

                if (leg.Tarsus != null) leg.Tarsus.localRotation = state.RestTarsus;
            }
        }

        private void UpdateSteps(float deltaTime)
        {
            var duration = pendingDuration;
            var lead = pendingLead;

            var threshold = Mathf.Max(stepReach * settleFraction, lead.magnitude * 2f);

            var swinging = false;

            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (!state.Swinging) continue;

                state.SwingTime += deltaTime / Mathf.Max(0.01f, state.SwingDuration);

                var landing = Ground(i, lead);

                if (state.SwingTime >= 1f)
                {
                    state.Swinging = false;
                    state.Planted = landing;
                    state.Current = landing;
                    continue;
                }

                var eased = Mathf.SmoothStep(0f, 1f, state.SwingTime);
                var position = Vector3.Lerp(state.SwingOrigin, landing, eased);

                position.y += Mathf.Sin(Mathf.PI * state.SwingTime) * state.Arc;

                state.Current = position;
                swinging = true;
            }

            if (Collapse > 0.5f) return;

            var bestGroup = -1;
            var bestError = threshold;

            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (state.Swinging) continue;

                var error = Error(i, lead);

                // A leg this far out of place has run out of reach; letting it wait for the
                // other group to land is what produces the splits.
                var overdue = error > threshold * 2.5f;

                if (swinging && !overdue) continue;
                if (error <= bestError) continue;

                bestError = error;
                bestGroup = legs[i].Group;
            }

            if (bestGroup < 0) return;

            for (var i = 0; i < states.Length; i++)
            {
                if (legs[i].Group != bestGroup) continue;

                var state = states[i];
                if (state.Swinging) continue;

                var error = Error(i, lead);
                if (error < threshold * 0.35f) continue;

                state.Swinging = true;
                state.SwingTime = 0f;
                state.SwingDuration = duration;
                state.SwingOrigin = state.Current;
                state.Arc = stepHeight * Mathf.Clamp01(error / stepReach + 0.35f);
            }
        }

        private float Error(int index, Vector3 lead)
        {
            var state = states[index];
            var desired = transform.TransformPoint(state.HomeLocal) + lead;
            var offset = desired - state.Current;

            offset.y = 0f;
            return offset.magnitude;
        }

        private Vector3 Ground(int index, Vector3 lead)
        {
            var state = states[index];
            var desired = transform.TransformPoint(state.HomeLocal) + lead;

            var probe = probes[index];

            if (probe.Resolved)
                return probe.Hit
                    ? new Vector3(desired.x, probe.Height + state.HomeLocal.y, desired.z)
                    : desired;

            // Deliberately not probed: too far to read, or collapsing. The flat answer costs
            // nothing and looks the same from there.
            if (probesSkipped) return desired;

            return GroundDirect(index, lead);
        }

        // The batch can only answer probes that were queued for it. A replant happens inside the
        // collect pass itself and an editor preview has no batch at all — both are rare enough to
        // pay for their own cast.
        private Vector3 GroundDirect(int index, Vector3 lead)
        {
            var state = states[index];
            var desired = transform.TransformPoint(state.HomeLocal) + lead;

            var origin = new Vector3(desired.x, transform.position.y + probeUp, desired.z);

            // Enemy and Player are both outside the walkable mask, so a swarm walks over the
            // floor rather than over each other's heads.
            if (Physics.Raycast(origin, Vector3.down, out var hit, probeUp + probeDown,
                    PhysicsLayers.WalkableMask, QueryTriggerInteraction.Ignore))
                return new Vector3(desired.x, hit.point.y + state.HomeLocal.y, desired.z);

            return desired;
        }

        private void UpdateBody(float deltaTime)
        {
            var targetHeight = 0f;
            var swing = 0f;

            var front = 0f;
            var back = 0f;
            var left = 0f;
            var right = 0f;

            var frontCount = 0;
            var backCount = 0;
            var leftCount = 0;
            var rightCount = 0;

            var frontSpan = 0f;
            var backSpan = 0f;
            var leftSpan = 0f;
            var rightSpan = 0f;

            var planted = 0;

            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];

                if (state.Swinging)
                {
                    swing = Mathf.Max(swing, Mathf.Sin(Mathf.PI * state.SwingTime));
                    continue;
                }

                // Only feet on the ground have anything to say about where the ground is. A leg
                // mid-swing is a metre in the air by design, and letting it vote lifts the body
                // every time the thing takes a step — a limp on flat floor.
                planted++;

                var home = transform.TransformPoint(state.HomeLocal);
                var lift = state.Current.y - home.y;

                targetHeight += lift;

                if (state.HomeLocal.z > 0.02f)
                {
                    front += lift;
                    frontSpan += state.HomeLocal.z;
                    frontCount++;
                }
                else if (state.HomeLocal.z < -0.02f)
                {
                    back += lift;
                    backSpan += state.HomeLocal.z;
                    backCount++;
                }

                if (state.HomeLocal.x > 0.02f)
                {
                    right += lift;
                    rightSpan += state.HomeLocal.x;
                    rightCount++;
                }
                else if (state.HomeLocal.x < -0.02f)
                {
                    left += lift;
                    leftSpan += state.HomeLocal.x;
                    leftCount++;
                }
            }

            // Nothing planted means everything is mid-air: keep the height it had rather than
            // dropping the body to zero for a frame.
            targetHeight = planted > 0
                ? Mathf.Clamp(targetHeight / planted, -stepReach, stepReach)
                : groundHeight;

            groundHeight = Mathf.Lerp(groundHeight, targetHeight,
                1f - Mathf.Exp(-bodyFollowSpeed * deltaTime));

            var local = transform.InverseTransformDirection(velocity);
            var localAcceleration = transform.InverseTransformDirection(acceleration);

            var pitch = Mathf.Clamp(local.z * leanPerSpeed, -maxLean, maxLean);
            var roll = Mathf.Clamp(-localAcceleration.x * leanPerAcceleration, -maxLean, maxLean);

            pitch += Slope(front, frontCount, frontSpan, back, backCount, backSpan);
            roll -= Slope(right, rightCount, rightSpan, left, leftCount, leftSpan);

            pitch = Mathf.Clamp(pitch + ExtraPitch + Collapse * collapseTilt, -90f, 90f);
            roll = Mathf.Clamp(roll + ExtraRoll, -90f, 90f);

            var height = groundHeight
                         + swing * bobAmplitude
                         + Mathf.Sin(Time.time * breatheFrequency * Mathf.PI * 2f + breathePhase) *
                         breatheAmplitude
                         - Collapse * collapseDrop;

            body.position += Vector3.up * height + BodyOffset;
            body.rotation = Quaternion.AngleAxis(pitch, transform.right) *
                            Quaternion.AngleAxis(roll, transform.forward) * body.rotation;
        }

        // Degrees the body turns to sit level between two sets of feet standing at different
        // heights. Either set missing — a biped has no front and back — means no answer, which
        // is 0 rather than a guess.
        private float Slope(float first, int firstCount, float firstSpan, float second,
            int secondCount, float secondSpan)
        {
            if (firstCount == 0 || secondCount == 0) return 0f;

            var span = Mathf.Abs(firstSpan / firstCount - secondSpan / secondCount);
            if (span < 0.01f) return 0f;

            var rise = first / firstCount - second / secondCount;
            var angle = Mathf.Atan2(rise, span) * Mathf.Rad2Deg;

            return Mathf.Clamp(-angle, -maxTilt, maxTilt);
        }

        private void SolveLegs()
        {
            for (var i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                var state = states[i];

                var foot = state.Current;

                state.Yaw = 0f;

                if (leg.Hip != null && hipYaw > 0f)
                {
                    var axis = body.up;
                    var rest = Vector3.ProjectOnPlane(
                        body.TransformVector(state.RestDirectionInBody), axis);
                    var current = Vector3.ProjectOnPlane(foot - leg.Hip.position, axis);

                    if (rest.sqrMagnitude > 1e-6f && current.sqrMagnitude > 1e-6f)
                    {
                        state.Yaw = Mathf.Clamp(Vector3.SignedAngle(rest, current, axis),
                            -hipYaw, hipYaw);
                        leg.Hip.rotation = Quaternion.AngleAxis(state.Yaw, axis) * leg.Hip.rotation;
                    }
                }

                var pole = state.PoleSpace.TransformPoint(state.PoleLocal);

                if (leg.Tarsus != null)
                {
                    // The ankle is placed a fixed offset behind the foot, which is exactly the
                    // tarsus's own length: the last segment is then a pure aim rather than a
                    // third unknown in the solve.
                    var ankle = foot + transform.TransformVector(state.TarsusOffsetLocal);

                    TwoBoneSolver.Solve(leg.Thigh, leg.Shin, leg.Tarsus, ankle, pole,
                        state.ThighLength, state.ShinLength);

                    TwoBoneSolver.Aim(leg.Tarsus, leg.Foot.position, foot);
                }
                else
                {
                    TwoBoneSolver.Solve(leg.Thigh, leg.Shin, leg.Foot, foot, pole,
                        state.ThighLength, state.ShinLength);
                }

                leg.Foot.rotation = Quaternion.AngleAxis(state.Yaw, transform.up) *
                                    transform.rotation * state.RestFoot;

                if (leg.Target != null) leg.Target.position = foot;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!ready) return;

            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];

                Gizmos.color = state.Swinging
                    ? new Color(0.95f, 0.75f, 0.2f)
                    : new Color(0.2f, 0.8f, 0.95f);

                Gizmos.DrawWireSphere(state.Current, 0.05f);
                Gizmos.DrawLine(transform.TransformPoint(state.HomeLocal), state.Current);
            }
        }
    }
}
