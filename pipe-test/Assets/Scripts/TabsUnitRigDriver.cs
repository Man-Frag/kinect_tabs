using UnityEngine;

/// <summary>
/// Drives the TABS unit Mixamo rig from MediaPipe body-tracking data.
///
/// Each bone's bind-pose rotation and pointing direction are captured once in
/// Start() and stored as a <see cref="BoneRest"/>.  At runtime a
/// <see cref="Quaternion.FromToRotation"/> maps the rest direction to the live
/// MediaPipe direction, then multiplied by the rest rotation to arrive at the
/// final bone rotation — no manual axis offsets required, and no Humanoid
/// avatar constraints involved.
///
/// During calibration the model displays the <b>reference pose</b> for the
/// current step, giving the user a visual guide to match.  Root position and
/// orientation still follow live tracking so the model stays on the person.
/// When calibration completes the model transitions smoothly to live tracking.
///
/// Attach to the root GameObject of the imported <c>Tabs_unit.fbx</c> model
/// (Generic rig, as set by the supplied .meta file).
/// </summary>
public class TabsUnitRigDriver : MonoBehaviour
{
    [Header("Tracking")]
    public UdpPoseReceiver receiver;
    public int playerId = 1;

    [Header("Mini-Rig Source")]
    [Tooltip("World-space joint positions are read from this visualiser. "
           + "Its skeleton can be hidden; the joint transforms still update.")]
    public MiniRigVisualizer miniRig;

    [Header("Root Motion")]
    public float positionSmoothing = 12.0f;
    public float rotationSmoothing = 10.0f;

    [Header("Bone Smoothing")]
    public float boneSmoothing = 18.0f;

    [Header("Bone Weights")]
    [Range(0f, 1f)] public float armWeight   = 1.0f;
    [Range(0f, 1f)] public float legWeight   = 1.0f;
    [Range(0f, 1f)] public float spineWeight = 0.6f;
    [Range(0f, 1f)] public float headWeight  = 0.35f;

    // Fine-tune rotations if needed (mostly for roll/twist).
    // Leave at zero — the FromToRotation approach handles axis alignment automatically.
    [Header("Extra Twist Offsets (degrees, usually not needed)")]
    public Vector3 upperArmTwist  = Vector3.zero;
    public Vector3 lowerArmTwist  = Vector3.zero;
    public Vector3 upperLegTwist  = Vector3.zero;
    public Vector3 lowerLegTwist  = Vector3.zero;
    public Vector3 spineTwist     = Vector3.zero;
    public Vector3 headTwist      = Vector3.zero;

    // -----------------------------------------------------------------------

    /// <summary>Bind-pose snapshot for one bone.</summary>
    private struct BoneRest
    {
        /// <summary>World-space rotation of the bone in the bind pose.</summary>
        public Quaternion rotation;
        /// <summary>World-space direction the bone points toward its child at bind.</summary>
        public Vector3 pointDir;
    }

    // Bone transforms found by name.
    private Transform _spine,  _spine1, _spine2;
    private Transform _neck,   _head;
    private Transform _lUpperArm, _lLowerArm;
    private Transform _rUpperArm, _rLowerArm;
    private Transform _lUpperLeg, _lLowerLeg;
    private Transform _rUpperLeg, _rLowerLeg;

    // Corresponding bind-pose snapshots.
    private BoneRest _spineR,     _spine1R,    _spine2R;
    private BoneRest _headR;
    private BoneRest _lUpperArmR, _lLowerArmR;
    private BoneRest _rUpperArmR, _rLowerArmR;
    private BoneRest _lUpperLegR, _lLowerLegR;
    private BoneRest _rUpperLegR, _rLowerLegR;

    private float   _baselineTorsoHeight = -1.0f;
    private Vector3 _baseModelScale      = Vector3.one;

    // -----------------------------------------------------------------------
    // Calibration reference poses
    //
    // Each sub-array defines joint offsets (in metres) from the model's hip
    // centre in LOCAL space (model facing +Z, Y-up, root at origin).
    //
    // Index layout:
    //   0  nose
    //   1  left_shoulder     2  right_shoulder
    //   3  left_elbow        4  right_elbow
    //   5  left_wrist        6  right_wrist
    //   7  left_hip          8  right_hip
    //   9  left_knee        10  right_knee
    //  11  left_ankle       12  right_ankle
    //
    // l_ joints at −X (model's left), r_ at +X (model's right).
    // -----------------------------------------------------------------------
    private static readonly Vector3[][] CalibRefs = new Vector3[][]
    {
        // ── Stage 0 ─ stand straight, arms and legs closed ──────────────────
        new Vector3[] {
            new Vector3( 0.00f,  0.70f,  0.00f), //  0  nose
            new Vector3(-0.23f,  0.45f,  0.00f), //  1  l_shoulder
            new Vector3( 0.23f,  0.45f,  0.00f), //  2  r_shoulder
            new Vector3(-0.23f,  0.15f,  0.00f), //  3  l_elbow   — arms hanging
            new Vector3( 0.23f,  0.15f,  0.00f), //  4  r_elbow
            new Vector3(-0.23f, -0.15f,  0.00f), //  5  l_wrist
            new Vector3( 0.23f, -0.15f,  0.00f), //  6  r_wrist
            new Vector3(-0.10f,  0.00f,  0.00f), //  7  l_hip
            new Vector3( 0.10f,  0.00f,  0.00f), //  8  r_hip
            new Vector3(-0.10f, -0.48f,  0.00f), //  9  l_knee
            new Vector3( 0.10f, -0.48f,  0.00f), // 10  r_knee
            new Vector3(-0.10f, -0.92f,  0.00f), // 11  l_ankle
            new Vector3( 0.10f, -0.92f,  0.00f), // 12  r_ankle
        },

        // ── Stage 1 ─ arms straight forward toward camera, legs closed ──────
        new Vector3[] {
            new Vector3( 0.00f,  0.70f,  0.00f),
            new Vector3(-0.23f,  0.45f,  0.00f),
            new Vector3( 0.23f,  0.45f,  0.00f),
            new Vector3(-0.23f,  0.45f,  0.40f), //  3  l_elbow  — forward
            new Vector3( 0.23f,  0.45f,  0.40f), //  4  r_elbow
            new Vector3(-0.23f,  0.45f,  0.80f), //  5  l_wrist  — at shoulder height, far forward
            new Vector3( 0.23f,  0.45f,  0.80f), //  6  r_wrist
            new Vector3(-0.10f,  0.00f,  0.00f),
            new Vector3( 0.10f,  0.00f,  0.00f),
            new Vector3(-0.10f, -0.48f,  0.00f),
            new Vector3( 0.10f, -0.48f,  0.00f),
            new Vector3(-0.10f, -0.92f,  0.00f),
            new Vector3( 0.10f, -0.92f,  0.00f),
        },

        // ── Stage 2 ─ T-pose, legs closed ───────────────────────────────────
        new Vector3[] {
            new Vector3( 0.00f,  0.70f,  0.00f),
            new Vector3(-0.23f,  0.45f,  0.00f),
            new Vector3( 0.23f,  0.45f,  0.00f),
            new Vector3(-0.60f,  0.45f,  0.00f), //  3  l_elbow  — wide
            new Vector3( 0.60f,  0.45f,  0.00f), //  4  r_elbow
            new Vector3(-0.95f,  0.45f,  0.00f), //  5  l_wrist  — full arm span
            new Vector3( 0.95f,  0.45f,  0.00f), //  6  r_wrist
            new Vector3(-0.10f,  0.00f,  0.00f),
            new Vector3( 0.10f,  0.00f,  0.00f),
            new Vector3(-0.10f, -0.48f,  0.00f),
            new Vector3( 0.10f, -0.48f,  0.00f),
            new Vector3(-0.10f, -0.92f,  0.00f),
            new Vector3( 0.10f, -0.92f,  0.00f),
        },

        // ── Stage 3 ─ T-pose, legs open (A-stance) ──────────────────────────
        new Vector3[] {
            new Vector3( 0.00f,  0.70f,  0.00f),
            new Vector3(-0.23f,  0.45f,  0.00f),
            new Vector3( 0.23f,  0.45f,  0.00f),
            new Vector3(-0.60f,  0.45f,  0.00f),
            new Vector3( 0.60f,  0.45f,  0.00f),
            new Vector3(-0.95f,  0.45f,  0.00f),
            new Vector3( 0.95f,  0.45f,  0.00f),
            new Vector3(-0.10f,  0.00f,  0.00f),
            new Vector3( 0.10f,  0.00f,  0.00f),
            new Vector3(-0.30f, -0.48f,  0.00f), //  9  l_knee  — legs spread
            new Vector3( 0.30f, -0.48f,  0.00f), // 10  r_knee
            new Vector3(-0.34f, -0.92f,  0.00f), // 11  l_ankle
            new Vector3( 0.34f, -0.92f,  0.00f), // 12  r_ankle
        },

        // ── Stage 4 ─ arms straight up, legs open ───────────────────────────
        new Vector3[] {
            new Vector3( 0.00f,  0.70f,  0.00f),
            new Vector3(-0.23f,  0.45f,  0.00f),
            new Vector3( 0.23f,  0.45f,  0.00f),
            new Vector3(-0.40f,  0.76f,  0.00f), //  3  l_elbow  — up-outward
            new Vector3( 0.40f,  0.76f,  0.00f), //  4  r_elbow
            new Vector3(-0.50f,  1.06f,  0.00f), //  5  l_wrist  — above head
            new Vector3( 0.50f,  1.06f,  0.00f), //  6  r_wrist
            new Vector3(-0.10f,  0.00f,  0.00f),
            new Vector3( 0.10f,  0.00f,  0.00f),
            new Vector3(-0.30f, -0.48f,  0.00f),
            new Vector3( 0.30f, -0.48f,  0.00f),
            new Vector3(-0.34f, -0.92f,  0.00f),
            new Vector3( 0.34f, -0.92f,  0.00f),
        },
    };

    // -----------------------------------------------------------------------

    private void Start()
    {
        // Disable Animator root motion (added by Unity during FBX import).
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
            anim.applyRootMotion = false;

        // Find every driven bone by its mixamorig: name.
        // Mixamo naming:
        //   LeftArm     = upper arm (shoulder→elbow)
        //   LeftForeArm = lower arm (elbow→wrist)
        //   LeftUpLeg   = upper leg (hip→knee)
        //   LeftLeg     = lower leg (knee→ankle)
        _spine     = FindBone("mixamorig:Spine");
        _spine1    = FindBone("mixamorig:Spine1");
        _spine2    = FindBone("mixamorig:Spine2");
        _neck      = FindBone("mixamorig:Neck");
        _head      = FindBone("mixamorig:Head");

        _lUpperArm = FindBone("mixamorig:LeftArm");
        _lLowerArm = FindBone("mixamorig:LeftForeArm");
        _rUpperArm = FindBone("mixamorig:RightArm");
        _rLowerArm = FindBone("mixamorig:RightForeArm");

        _lUpperLeg = FindBone("mixamorig:LeftUpLeg");
        _lLowerLeg = FindBone("mixamorig:LeftLeg");
        _rUpperLeg = FindBone("mixamorig:RightUpLeg");
        _rLowerLeg = FindBone("mixamorig:RightLeg");

        // Capture bind-pose rotations and pointing directions.
        // Must happen in Start(), before any LateUpdate moves the bones.
        _spineR     = Capture(_spine,     _spine1);
        _spine1R    = Capture(_spine1,    _spine2);
        _spine2R    = Capture(_spine2,    FindBone("mixamorig:Neck"));
        _headR      = Capture(_head,      FindBone("mixamorig:HeadTop_End"));

        _lUpperArmR = Capture(_lUpperArm, _lLowerArm);
        _lLowerArmR = Capture(_lLowerArm, FindBone("mixamorig:LeftHand"));
        _rUpperArmR = Capture(_rUpperArm, _rLowerArm);
        _rLowerArmR = Capture(_rLowerArm, FindBone("mixamorig:RightHand"));

        _lUpperLegR = Capture(_lUpperLeg, _lLowerLeg);
        _lLowerLegR = Capture(_lLowerLeg, FindBone("mixamorig:LeftFoot"));
        _rUpperLegR = Capture(_rUpperLeg, _rLowerLeg);
        _rLowerLegR = Capture(_rLowerLeg, FindBone("mixamorig:RightFoot"));

        if (_spine == null)
            Debug.LogWarning(
                "TabsUnitRigDriver: 'mixamorig:Spine' not found. " +
                "Check that Tabs_unit.fbx is in Assets/Models and imported correctly.", this);

        if (miniRig == null)
            miniRig = Object.FindFirstObjectByType<MiniRigVisualizer>();
        if (receiver == null)
            receiver = Object.FindFirstObjectByType<UdpPoseReceiver>();

        _baseModelScale = transform.localScale;
    }

    // LateUpdate: MiniRig's Update() has already moved joint transforms for
    // this frame, so we read current positions here without a one-frame lag.
    private void LateUpdate()
    {
        if (miniRig == null)
            return;

        // ---- Live positions — always used for root motion ----
        Vector3 lHipLive  = JP("left_hip");
        Vector3 rHipLive  = JP("right_hip");
        Vector3 lShLive   = JP("left_shoulder");
        Vector3 rShLive   = JP("right_shoulder");

        Vector3 hipCenter      = (lHipLive + rHipLive) * 0.5f;
        Vector3 shoulderCenter = (lShLive  + rShLive)  * 0.5f;

        // ---- Root: position tracks hip centre ----
        transform.position = Vector3.Lerp(
            transform.position,
            hipCenter,
            Time.deltaTime * positionSmoothing);

        // ---- Root: orientation from shoulder axis ----
        Vector3 shoulderRight = rShLive - lShLive;
        shoulderRight.y = 0.0f;
        if (shoulderRight.sqrMagnitude > 0.0001f)
        {
            Vector3 fwd = Vector3.Cross(Vector3.up, shoulderRight.normalized);
            if (fwd.sqrMagnitude > 0.0001f)
            {
                Quaternion tgtRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, tgtRot, Time.deltaTime * rotationSmoothing);
            }
        }

        // ---- Root: scale proportional to torso height ----
        float torsoH = Vector3.Distance(shoulderCenter, hipCenter);
        if (torsoH > 0.0001f && _baselineTorsoHeight < 0.0f)
            _baselineTorsoHeight = torsoH;

        if (_baselineTorsoHeight > 0.0001f)
        {
            float s = Mathf.Clamp(torsoH / _baselineTorsoHeight, 0.65f, 1.5f);
            transform.localScale = Vector3.Lerp(
                transform.localScale, _baseModelScale * s, Time.deltaTime * 4.0f);
        }

        // ---- Choose joint positions for bone driving ----
        // During calibration:  use the reference pose for the current step
        //                      so the model shows the user what position to adopt.
        // After calibration:   use live tracking from the MiniRig.
        Vector3 nose, lShoulder, rShoulder;
        Vector3 lElbow, rElbow, lWrist, rWrist;
        Vector3 lHip, rHip, lKnee, rKnee, lAnkle, rAnkle;

        if (miniRig.IsCalibrating)
        {
            int       step = Mathf.Clamp(miniRig.CalibrationStep, 0, CalibRefs.Length - 1);
            Vector3[] r    = CalibRefs[step];
            Vector3   pos  = transform.position;   // hip centre (post-smoothing this frame)
            Quaternion rot = transform.rotation;   // model orientation (post-smoothing)

            // Offsets are in model-local space; rotate them into world space.
            nose      = pos + rot * r[0];
            lShoulder = pos + rot * r[1];  rShoulder = pos + rot * r[2];
            lElbow    = pos + rot * r[3];  rElbow    = pos + rot * r[4];
            lWrist    = pos + rot * r[5];  rWrist    = pos + rot * r[6];
            lHip      = pos + rot * r[7];  rHip      = pos + rot * r[8];
            lKnee     = pos + rot * r[9];  rKnee     = pos + rot * r[10];
            lAnkle    = pos + rot * r[11]; rAnkle    = pos + rot * r[12];
        }
        else
        {
            // Negate X: MiniRig places person's left at world +X but the model's
            // LeftArm bind pose points toward −X.  Without the flip the bone drives
            // to the wrong side (opposite direction on the horizontal axis).
            // Y and Z are unaffected — up/down and depth were already correct.
            nose      = FlipX(JP("nose"));
            lShoulder = FlipX(lShLive);            rShoulder = FlipX(rShLive);
            lElbow    = FlipX(JP("left_elbow"));   rElbow    = FlipX(JP("right_elbow"));
            lWrist    = FlipX(JP("left_wrist"));   rWrist    = FlipX(JP("right_wrist"));
            lHip      = FlipX(lHipLive);           rHip      = FlipX(rHipLive);
            lKnee     = FlipX(JP("left_knee"));    rKnee     = FlipX(JP("right_knee"));
            lAnkle    = FlipX(JP("left_ankle"));   rAnkle    = FlipX(JP("right_ankle"));
        }

        // Recompute centres from the chosen joint set.
        hipCenter      = (lHip      + rHip)      * 0.5f;
        shoulderCenter = (lShoulder + rShoulder)  * 0.5f;

        // ---- Spine — weight tapers up the chain ----
        Vector3 torsoDir = shoulderCenter - hipCenter;
        RB(_spine,     _spineR,     torsoDir,             spineWeight * 1.00f, spineTwist);
        RB(_spine1,    _spine1R,    torsoDir,             spineWeight * 0.65f, spineTwist);
        RB(_spine2,    _spine2R,    torsoDir,             spineWeight * 0.35f, spineTwist);

        // ---- Head ----
        RB(_head, _headR, nose - shoulderCenter, headWeight, headTwist);

        // ---- Arms ----
        RB(_lUpperArm, _lUpperArmR, lElbow - lShoulder, armWeight, upperArmTwist);
        RB(_lLowerArm, _lLowerArmR, lWrist - lElbow,    armWeight, lowerArmTwist);
        RB(_rUpperArm, _rUpperArmR, rElbow - rShoulder, armWeight, upperArmTwist);
        RB(_rLowerArm, _rLowerArmR, rWrist - rElbow,    armWeight, lowerArmTwist);

        // ---- Legs ----
        RB(_lUpperLeg, _lUpperLegR, lKnee  - lHip,   legWeight, upperLegTwist);
        RB(_lLowerLeg, _lLowerLegR, lAnkle - lKnee,  legWeight, lowerLegTwist);
        RB(_rUpperLeg, _rUpperLegR, rKnee  - rHip,   legWeight, upperLegTwist);
        RB(_rLowerLeg, _rLowerLegR, rAnkle - rKnee,  legWeight, lowerLegTwist);
    }

    // -----------------------------------------------------------------------

    private Vector3 JP(string name)
    {
        Transform t = miniRig.GetJointTransform(name);
        return t != null ? t.position : Vector3.zero;
    }

    /// <summary>
    /// Negates the X component of a world-space joint position so that live
    /// MiniRig coordinates align with the model's anatomical convention.
    /// The MiniRig places person's left limbs at world +X, but the Mixamo
    /// skeleton has LeftArm/LeftUpLeg pointing toward −X in the bind pose.
    /// Flipping X makes the direction vectors consistent with CalibRefs and
    /// with the captured bind-pose directions, so bones move the correct way.
    /// Root-motion code uses the raw (unflipped) positions — those are fine.
    /// </summary>
    private static Vector3 FlipX(Vector3 v) => new Vector3(-v.x, v.y, v.z);

    /// <summary>
    /// Rotates <paramref name="bone"/> so that its bind-pose pointing direction
    /// aligns with <paramref name="targetDir"/> in world space.
    /// Uses FromToRotation rather than LookRotation so no manual axis offset is
    /// needed — the bind-pose orientation fully defines the bone's natural axes.
    /// </summary>
    private void RB(
        Transform bone,
        BoneRest  rest,
        Vector3   targetDir,
        float     weight,
        Vector3   twistOffset)
    {
        if (bone == null || targetDir.sqrMagnitude < 0.0001f)
            return;

        // Rotate FROM rest direction TO live direction, then apply to rest rotation.
        Quaternion fromTo = Quaternion.FromToRotation(rest.pointDir, targetDir.normalized);
        Quaternion target = fromTo * rest.rotation * Quaternion.Euler(twistOffset);

        bone.rotation = Quaternion.Slerp(
            bone.rotation,
            target,
            Time.deltaTime * boneSmoothing * weight);
    }

    /// <summary>
    /// Records a bone's bind-pose world rotation and the world-space direction
    /// it points toward <paramref name="child"/> at that pose.
    /// Call in Start() before any LateUpdate moves the bones.
    /// </summary>
    private static BoneRest Capture(Transform bone, Transform child)
    {
        if (bone == null)
            return new BoneRest { rotation = Quaternion.identity, pointDir = Vector3.up };

        Vector3 dir;
        if (child != null)
        {
            Vector3 d = child.position - bone.position;
            dir = d.sqrMagnitude > 1e-6f ? d.normalized : bone.rotation * Vector3.up;
        }
        else
        {
            // No child — fall back to local Y (Blender/Mixamo convention).
            dir = bone.rotation * Vector3.up;
        }

        return new BoneRest { rotation = bone.rotation, pointDir = dir };
    }

    /// <summary>Depth-first search for a Transform with the given name.</summary>
    private Transform FindBone(string boneName)
    {
        return FindInHierarchy(transform, boneName);
    }

    private static Transform FindInHierarchy(Transform node, string boneName)
    {
        if (node.name == boneName)
            return node;

        for (int i = 0; i < node.childCount; i++)
        {
            Transform found = FindInHierarchy(node.GetChild(i), boneName);
            if (found != null)
                return found;
        }

        return null;
    }
}
