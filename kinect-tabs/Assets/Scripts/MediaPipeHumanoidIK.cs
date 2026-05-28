using UnityEngine;

public class MediaPipeHumanoidIK : MonoBehaviour
{
    [Header("Tracking")]
    public UdpPoseReceiver receiver;
    public int playerId = 1;

    [Header("Animator")]
    public Animator animator;

    [Header("Bone Rotation Drive")]
    public bool driveBoneRotations = true;
    public float boneRotationSmoothing = 16.0f;
    public bool preferPacketBoneVectors = true;

    [Range(0.0f, 1.0f)]
    public float minBoneVectorConfidence = 0.25f;

    [Tooltip("If false, arm/leg bone rotations are skipped when IK weights for those limbs are active to avoid double-driving.")]
    public bool blendBoneRotationsWithIK = false;

    [Range(0.0f, 1.0f)]
    public float armRotationWeight = 0.9f;

    [Range(0.0f, 1.0f)]
    public float legRotationWeight = 0.8f;

    [Range(0.0f, 1.0f)]
    public float headRotationWeight = 0.35f;

    public Vector3 upperArmAxisOffset = new Vector3(0, 0, 0);
    public Vector3 lowerArmAxisOffset = new Vector3(0, 0, 0);
    public Vector3 upperLegAxisOffset = new Vector3(0, 0, 0);
    public Vector3 lowerLegAxisOffset = new Vector3(0, 0, 0);
    public Vector3 headAxisOffset = new Vector3(0, 0, 0);

    [Header("Mirror / Swap")]
    public bool swapLeftRight = false;
    public bool mirrorX = false;

    [Header("Body Mapping")]
    public float horizontalScale = 3.0f;
    public float verticalScale = 3.0f;
    public float horizontalMotionGain = 1.0f;
    public float verticalMotionGain = 1.0f;
    public float depthScale = 1.0f;
    public float depthMotionGain = 2.8f;
    public float forwardOffset = 0.7f;
    public float bodyYOffset = 1.0f;

    [Header("Root Movement")]
    public bool moveRoot = true;
    public float rootMoveScale = 3.0f;
    public float rootSmoothing = 8.0f;

    [Header("IK Weights")]
    [Range(0.0f, 1.0f)]
    public float handWeight = 1.0f;

    [Range(0.0f, 1.0f)]
    public float elbowHintWeight = 0.8f;

    [Range(0.0f, 1.0f)]
    public float footWeight = 0.6f;

    [Range(0.0f, 1.0f)]
    public float kneeHintWeight = 0.6f;

    [Range(0.0f, 1.0f)]
    public float lookWeight = 0.25f;

    [Header("Smoothing")]
    public float targetSmoothing = 18.0f;

    [Header("Debug Target Spheres")]
    public bool showDebugTargets = true;
    public float debugSphereSize = 0.12f;

    private Vector3 leftHandTarget;
    private Vector3 rightHandTarget;
    private Vector3 leftElbowHint;
    private Vector3 rightElbowHint;

    private Vector3 leftFootTarget;
    private Vector3 rightFootTarget;
    private Vector3 leftKneeHint;
    private Vector3 rightKneeHint;

    private Vector3 headTarget;

    private Transform leftHandSphere;
    private Transform rightHandSphere;
    private Transform leftFootSphere;
    private Transform rightFootSphere;

    private Transform headBone;
    private Transform leftUpperArmBone;
    private Transform leftLowerArmBone;
    private Transform rightUpperArmBone;
    private Transform rightLowerArmBone;
    private Transform leftUpperLegBone;
    private Transform leftLowerLegBone;
    private Transform rightUpperLegBone;
    private Transform rightLowerLegBone;

    private Transform leftHandBone;
    private Transform rightHandBone;
    private Transform leftFootBone;
    private Transform rightFootBone;

    private Vector3 leftUpperArmAimAxis = Vector3.right;
    private Vector3 rightUpperArmAimAxis = Vector3.left;
    private Vector3 leftLowerArmAimAxis = Vector3.right;
    private Vector3 rightLowerArmAimAxis = Vector3.left;
    private Vector3 leftUpperLegAimAxis = Vector3.down;
    private Vector3 rightUpperLegAimAxis = Vector3.down;
    private Vector3 leftLowerLegAimAxis = Vector3.down;
    private Vector3 rightLowerLegAimAxis = Vector3.down;
    private Vector3 headAimAxis = Vector3.forward;

    private PosePacket latestPacket;
    private PlayerPose latestPlayer;

    private bool warnedMissingBones;
    private float lastIkCallbackTime;

    private Quaternion headRestLocalRotation;
    private Quaternion leftUpperArmRestLocalRotation;
    private Quaternion leftLowerArmRestLocalRotation;
    private Quaternion rightUpperArmRestLocalRotation;
    private Quaternion rightLowerArmRestLocalRotation;
    private Quaternion leftUpperLegRestLocalRotation;
    private Quaternion leftLowerLegRestLocalRotation;
    private Quaternion rightUpperLegRestLocalRotation;
    private Quaternion rightLowerLegRestLocalRotation;

    private bool hasPose;

    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError("MediaPipeHumanoidIK: Animator missing.");
            return;
        }

        if (animator.avatar == null)
        {
            Debug.LogError("MediaPipeHumanoidIK: Animator has no Avatar.");
        }
        else
        {
            Debug.Log("Avatar is human: " + animator.avatar.isHuman);
            Debug.Log("Avatar is valid: " + animator.avatar.isValid);
        }

        DisableConflictingDrivers();

        headBone = animator.GetBoneTransform(HumanBodyBones.Head);

        leftUpperArmBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArmBone = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightUpperArmBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArmBone = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

        leftUpperLegBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        leftLowerLegBone = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightUpperLegBone = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rightLowerLegBone = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

        leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        leftFootBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFootBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        CacheBoneAimAxes();
        CacheRestLocalRotations();

        if (swapLeftRight)
        {
            Debug.LogWarning("MediaPipeHumanoidIK: swapLeftRight was enabled in scene data; forcing false for canonical non-mirrored UDP input.");
            swapLeftRight = false;
        }

        ValidateBoneBindings();

        if (showDebugTargets)
        {
            leftHandSphere = CreateDebugSphere("IK Left Hand Target", Color.yellow);
            rightHandSphere = CreateDebugSphere("IK Right Hand Target", Color.yellow);
            leftFootSphere = CreateDebugSphere("IK Left Foot Target", Color.green);
            rightFootSphere = CreateDebugSphere("IK Right Foot Target", Color.green);
        }
    }

    private Transform CreateDebugSphere(string name, Color color)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.localScale = Vector3.one * debugSphereSize;

        Collider collider = sphere.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = sphere.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = color;
        }

        return sphere.transform;
    }

    private void Update()
    {
        if (receiver == null || animator == null)
        {
            return;
        }

        PosePacket packet = receiver.GetLatestPacket();

        if (packet == null || packet.frame == null)
        {
            return;
        }

        PlayerPose player = receiver.GetPlayer(playerId);

        if (player == null || player.joints == null)
        {
            return;
        }

        latestPacket = packet;
        latestPlayer = player;
        hasPose = true;

        if (moveRoot)
        {
            MoveRoot(packet, player);
        }

        UpdateIKTargets(packet, player);

        if (showDebugTargets)
        {
            UpdateDebugSpheres();
        }
    }

    private void LateUpdate()
    {
        if (!warnedMissingBones)
        {
            ValidateBoneBindings();
        }

        if (Time.time - lastIkCallbackTime > 2.0f)
        {
            Debug.LogWarning("MediaPipeHumanoidIK: OnAnimatorIK is not being called. Ensure Animator has a valid controller state and IK Pass enabled.");
            lastIkCallbackTime = Time.time;
        }

    }

    private void MoveRoot(PosePacket packet, PlayerPose player)
    {
        JointPoint leftHip = player.joints.left_hip;
        JointPoint rightHip = player.joints.right_hip;

        if (leftHip == null || rightHip == null)
        {
            return;
        }

        float hipX = (leftHip.x + rightHip.x) * 0.5f;
        float normalizedX = hipX / packet.frame.width;
        float centeredX = normalizedX - 0.5f;

        if (mirrorX)
        {
            centeredX = -centeredX;
        }

        float worldX = centeredX * rootMoveScale;

        Vector3 targetPosition = new Vector3(
            worldX,
            transform.position.y,
            transform.position.z
        );

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * rootSmoothing
        );
    }

    private void UpdateIKTargets(PosePacket packet, PlayerPose player)
    {
        JointPoint leftWrist = player.joints.left_wrist;
        JointPoint rightWrist = player.joints.right_wrist;
        JointPoint leftElbow = player.joints.left_elbow;
        JointPoint rightElbow = player.joints.right_elbow;

        JointPoint leftAnkle = player.joints.left_ankle;
        JointPoint rightAnkle = player.joints.right_ankle;
        JointPoint leftKnee = player.joints.left_knee;
        JointPoint rightKnee = player.joints.right_knee;

        if (swapLeftRight)
        {
            leftWrist = player.joints.right_wrist;
            rightWrist = player.joints.left_wrist;

            leftElbow = player.joints.right_elbow;
            rightElbow = player.joints.left_elbow;

            leftAnkle = player.joints.right_ankle;
            rightAnkle = player.joints.left_ankle;

            leftKnee = player.joints.right_knee;
            rightKnee = player.joints.left_knee;
        }

        Vector2 bodyCenter = GetBodyCenter(player);
        float bodySize = GetBodySize(player);
        float bodyDepthCenter = GetBodyDepthCenter(player);

        UpdateTarget(packet, leftWrist, bodyCenter, bodySize, bodyDepthCenter, ref leftHandTarget);
        UpdateTarget(packet, rightWrist, bodyCenter, bodySize, bodyDepthCenter, ref rightHandTarget);

        UpdateTarget(packet, leftElbow, bodyCenter, bodySize, bodyDepthCenter, ref leftElbowHint);
        UpdateTarget(packet, rightElbow, bodyCenter, bodySize, bodyDepthCenter, ref rightElbowHint);

        UpdateTarget(packet, leftAnkle, bodyCenter, bodySize, bodyDepthCenter, ref leftFootTarget);
        UpdateTarget(packet, rightAnkle, bodyCenter, bodySize, bodyDepthCenter, ref rightFootTarget);

        UpdateTarget(packet, leftKnee, bodyCenter, bodySize, bodyDepthCenter, ref leftKneeHint);
        UpdateTarget(packet, rightKnee, bodyCenter, bodySize, bodyDepthCenter, ref rightKneeHint);

        UpdateTarget(packet, player.joints.nose, bodyCenter, bodySize, bodyDepthCenter, ref headTarget);
    }

    private Vector2 GetBodyCenter(PlayerPose player)
    {
        float x =
            player.joints.left_hip.x +
            player.joints.right_hip.x +
            player.joints.left_shoulder.x +
            player.joints.right_shoulder.x;

        float y =
            player.joints.left_hip.y +
            player.joints.right_hip.y +
            player.joints.left_shoulder.y +
            player.joints.right_shoulder.y;

        return new Vector2(x * 0.25f, y * 0.25f);
    }

    private float GetBodySize(PlayerPose player)
    {
        Vector2 leftShoulder = new Vector2(player.joints.left_shoulder.x, player.joints.left_shoulder.y);
        Vector2 rightShoulder = new Vector2(player.joints.right_shoulder.x, player.joints.right_shoulder.y);

        Vector2 leftHip = new Vector2(player.joints.left_hip.x, player.joints.left_hip.y);
        Vector2 rightHip = new Vector2(player.joints.right_hip.x, player.joints.right_hip.y);

        Vector2 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
        Vector2 hipCenter = (leftHip + rightHip) * 0.5f;

        float torsoHeight = Vector2.Distance(shoulderCenter, hipCenter);
        float shoulderWidth = Vector2.Distance(leftShoulder, rightShoulder);

        return Mathf.Max(torsoHeight, shoulderWidth, 1.0f);
    }

    private float GetBodyDepthCenter(PlayerPose player)
    {
        return (
            player.joints.left_hip.z +
            player.joints.right_hip.z +
            player.joints.left_shoulder.z +
            player.joints.right_shoulder.z
        ) * 0.25f;
    }

    private void UpdateTarget(
        PosePacket packet,
        JointPoint joint,
        Vector2 bodyCenter,
        float bodySize,
        float bodyDepthCenter,
        ref Vector3 target
    )
    {
        if (joint == null)
        {
            return;
        }

        Vector3 mappedPosition = JointToAvatarWorld(joint, bodyCenter, bodySize, bodyDepthCenter);

        target = Vector3.Lerp(
            target,
            mappedPosition,
            Time.deltaTime * targetSmoothing
        );
    }

    private Vector3 JointToAvatarWorld(JointPoint joint, Vector2 bodyCenter, float bodySize, float bodyDepthCenter)
    {
        float relativeX = ((joint.x - bodyCenter.x) / bodySize) * horizontalMotionGain;
        float relativeY = (-(joint.y - bodyCenter.y) / bodySize) * verticalMotionGain;

        if (mirrorX)
        {
            relativeX = -relativeX;
        }

        float relativeZ = -(joint.z - bodyDepthCenter) * depthScale * depthMotionGain;

        Vector3 localTarget = new Vector3(
            relativeX * horizontalScale,
            relativeY * verticalScale + bodyYOffset,
            relativeZ + forwardOffset
        );

        return transform.TransformPoint(localTarget);
    }

    private void UpdateDebugSpheres()
    {
        if (leftHandSphere != null)
        {
            leftHandSphere.position = leftHandTarget;
        }

        if (rightHandSphere != null)
        {
            rightHandSphere.position = rightHandTarget;
        }

        if (leftFootSphere != null)
        {
            leftFootSphere.position = leftFootTarget;
        }

        if (rightFootSphere != null)
        {
            rightFootSphere.position = rightFootTarget;
        }
    }

    private void ApplyBoneRotations(PosePacket packet, PlayerPose player)
    {
        if (preferPacketBoneVectors && TryApplyBoneRotationsFromPacketVectors(player))
        {
            return;
        }

        JointPoint leftShoulderJoint = player.joints.left_shoulder;
        JointPoint rightShoulderJoint = player.joints.right_shoulder;
        JointPoint leftElbowJoint = player.joints.left_elbow;
        JointPoint rightElbowJoint = player.joints.right_elbow;
        JointPoint leftWristJoint = player.joints.left_wrist;
        JointPoint rightWristJoint = player.joints.right_wrist;

        JointPoint leftHipJoint = player.joints.left_hip;
        JointPoint rightHipJoint = player.joints.right_hip;
        JointPoint leftKneeJoint = player.joints.left_knee;
        JointPoint rightKneeJoint = player.joints.right_knee;
        JointPoint leftAnkleJoint = player.joints.left_ankle;
        JointPoint rightAnkleJoint = player.joints.right_ankle;

        if (swapLeftRight)
        {
            leftShoulderJoint = player.joints.right_shoulder;
            rightShoulderJoint = player.joints.left_shoulder;
            leftElbowJoint = player.joints.right_elbow;
            rightElbowJoint = player.joints.left_elbow;
            leftWristJoint = player.joints.right_wrist;
            rightWristJoint = player.joints.left_wrist;

            leftHipJoint = player.joints.right_hip;
            rightHipJoint = player.joints.left_hip;
            leftKneeJoint = player.joints.right_knee;
            rightKneeJoint = player.joints.left_knee;
            leftAnkleJoint = player.joints.right_ankle;
            rightAnkleJoint = player.joints.left_ankle;
        }

        Vector2 bodyCenter = GetBodyCenter(player);
        float bodySize = GetBodySize(player);
        float bodyDepthCenter = GetBodyDepthCenter(player);

        Vector3 leftShoulder = JointToAvatarWorld(leftShoulderJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 rightShoulder = JointToAvatarWorld(rightShoulderJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 leftElbow = JointToAvatarWorld(leftElbowJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 rightElbow = JointToAvatarWorld(rightElbowJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 leftWrist = JointToAvatarWorld(leftWristJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 rightWrist = JointToAvatarWorld(rightWristJoint, bodyCenter, bodySize, bodyDepthCenter);

        Vector3 leftHip = JointToAvatarWorld(leftHipJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 rightHip = JointToAvatarWorld(rightHipJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 leftKnee = JointToAvatarWorld(leftKneeJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 rightKnee = JointToAvatarWorld(rightKneeJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 leftAnkle = JointToAvatarWorld(leftAnkleJoint, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 rightAnkle = JointToAvatarWorld(rightAnkleJoint, bodyCenter, bodySize, bodyDepthCenter);

        Vector3 nose = JointToAvatarWorld(player.joints.nose, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;

        bool armIkActive = handWeight > 0.01f;
        bool legIkActive = footWeight > 0.01f;

        if (blendBoneRotationsWithIK || !armIkActive)
        {
            RotateBoneToward(leftUpperArmBone, leftUpperArmAimAxis, leftElbow - leftShoulder, armRotationWeight, upperArmAxisOffset, leftUpperArmRestLocalRotation);
            RotateBoneToward(leftLowerArmBone, leftLowerArmAimAxis, leftWrist - leftElbow, armRotationWeight, lowerArmAxisOffset, leftLowerArmRestLocalRotation);
            RotateBoneToward(rightUpperArmBone, rightUpperArmAimAxis, rightElbow - rightShoulder, armRotationWeight, upperArmAxisOffset, rightUpperArmRestLocalRotation);
            RotateBoneToward(rightLowerArmBone, rightLowerArmAimAxis, rightWrist - rightElbow, armRotationWeight, lowerArmAxisOffset, rightLowerArmRestLocalRotation);
        }

        if (blendBoneRotationsWithIK || !legIkActive)
        {
            RotateBoneToward(leftUpperLegBone, leftUpperLegAimAxis, leftKnee - leftHip, legRotationWeight, upperLegAxisOffset, leftUpperLegRestLocalRotation);
            RotateBoneToward(leftLowerLegBone, leftLowerLegAimAxis, leftAnkle - leftKnee, legRotationWeight, lowerLegAxisOffset, leftLowerLegRestLocalRotation);
            RotateBoneToward(rightUpperLegBone, rightUpperLegAimAxis, rightKnee - rightHip, legRotationWeight, upperLegAxisOffset, rightUpperLegRestLocalRotation);
            RotateBoneToward(rightLowerLegBone, rightLowerLegAimAxis, rightAnkle - rightKnee, legRotationWeight, lowerLegAxisOffset, rightLowerLegRestLocalRotation);
        }

        RotateBoneToward(headBone, headAimAxis, nose - shoulderCenter, headRotationWeight, headAxisOffset, headRestLocalRotation);
    }

    private bool TryApplyBoneRotationsFromPacketVectors(PlayerPose player)
    {
        if (player == null || player.bones == null)
        {
            return false;
        }

        BoneVector leftUpperArm = swapLeftRight ? player.bones.right_upper_arm : player.bones.left_upper_arm;
        BoneVector leftLowerArm = swapLeftRight ? player.bones.right_lower_arm : player.bones.left_lower_arm;
        BoneVector rightUpperArm = swapLeftRight ? player.bones.left_upper_arm : player.bones.right_upper_arm;
        BoneVector rightLowerArm = swapLeftRight ? player.bones.left_lower_arm : player.bones.right_lower_arm;

        BoneVector leftUpperLeg = swapLeftRight ? player.bones.right_upper_leg : player.bones.left_upper_leg;
        BoneVector leftLowerLeg = swapLeftRight ? player.bones.right_lower_leg : player.bones.left_lower_leg;
        BoneVector rightUpperLeg = swapLeftRight ? player.bones.left_upper_leg : player.bones.right_upper_leg;
        BoneVector rightLowerLeg = swapLeftRight ? player.bones.left_lower_leg : player.bones.right_lower_leg;

        Vector3 leftUpperArmDirection = BoneVectorToWorldDirection(leftUpperArm);
        Vector3 leftLowerArmDirection = BoneVectorToWorldDirection(leftLowerArm);
        Vector3 rightUpperArmDirection = BoneVectorToWorldDirection(rightUpperArm);
        Vector3 rightLowerArmDirection = BoneVectorToWorldDirection(rightLowerArm);

        Vector3 leftUpperLegDirection = BoneVectorToWorldDirection(leftUpperLeg);
        Vector3 leftLowerLegDirection = BoneVectorToWorldDirection(leftLowerLeg);
        Vector3 rightUpperLegDirection = BoneVectorToWorldDirection(rightUpperLeg);
        Vector3 rightLowerLegDirection = BoneVectorToWorldDirection(rightLowerLeg);
        Vector3 headDirection = BoneVectorToWorldDirection(player.bones.head);

        bool armIkActive = handWeight > 0.01f;
        bool legIkActive = footWeight > 0.01f;
        bool appliedAny = false;

        if (blendBoneRotationsWithIK || !armIkActive)
        {
            appliedAny |= TryRotateFromDirection(leftUpperArmBone, leftUpperArmAimAxis, leftUpperArmDirection, armRotationWeight, upperArmAxisOffset, leftUpperArmRestLocalRotation);
            appliedAny |= TryRotateFromDirection(leftLowerArmBone, leftLowerArmAimAxis, leftLowerArmDirection, armRotationWeight, lowerArmAxisOffset, leftLowerArmRestLocalRotation);
            appliedAny |= TryRotateFromDirection(rightUpperArmBone, rightUpperArmAimAxis, rightUpperArmDirection, armRotationWeight, upperArmAxisOffset, rightUpperArmRestLocalRotation);
            appliedAny |= TryRotateFromDirection(rightLowerArmBone, rightLowerArmAimAxis, rightLowerArmDirection, armRotationWeight, lowerArmAxisOffset, rightLowerArmRestLocalRotation);
        }

        if (blendBoneRotationsWithIK || !legIkActive)
        {
            appliedAny |= TryRotateFromDirection(leftUpperLegBone, leftUpperLegAimAxis, leftUpperLegDirection, legRotationWeight, upperLegAxisOffset, leftUpperLegRestLocalRotation);
            appliedAny |= TryRotateFromDirection(leftLowerLegBone, leftLowerLegAimAxis, leftLowerLegDirection, legRotationWeight, lowerLegAxisOffset, leftLowerLegRestLocalRotation);
            appliedAny |= TryRotateFromDirection(rightUpperLegBone, rightUpperLegAimAxis, rightUpperLegDirection, legRotationWeight, upperLegAxisOffset, rightUpperLegRestLocalRotation);
            appliedAny |= TryRotateFromDirection(rightLowerLegBone, rightLowerLegAimAxis, rightLowerLegDirection, legRotationWeight, lowerLegAxisOffset, rightLowerLegRestLocalRotation);
        }

        appliedAny |= TryRotateFromDirection(headBone, headAimAxis, headDirection, headRotationWeight, headAxisOffset, headRestLocalRotation);
        return appliedAny;
    }

    private bool TryRotateFromDirection(
        Transform bone,
        Vector3 aimAxis,
        Vector3 direction,
        float weight,
        Vector3 axisOffset,
        Quaternion restLocalRotation
    )
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        RotateBoneToward(bone, aimAxis, direction, weight, axisOffset, restLocalRotation);
        return true;
    }

    private Vector3 BoneVectorToWorldDirection(BoneVector boneVector)
    {
        if (boneVector == null || boneVector.confidence < minBoneVectorConfidence)
        {
            return Vector3.zero;
        }

        Vector3 localDirection = new Vector3(
            boneVector.x,
            -boneVector.y,
            -boneVector.z
        );

        if (mirrorX)
        {
            localDirection.x = -localDirection.x;
        }

        if (localDirection.sqrMagnitude < 0.000001f)
        {
            return Vector3.zero;
        }

        return transform.TransformDirection(localDirection.normalized);
    }

    private void DisableConflictingDrivers()
    {
        MediaPipeHumanoidAvatar[] avatarDrivers = FindObjectsByType<MediaPipeHumanoidAvatar>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (MediaPipeHumanoidAvatar driver in avatarDrivers)
        {
            if (driver == null || !driver.enabled)
            {
                continue;
            }

            Animator driverAnimator = driver.animator != null ? driver.animator : driver.GetComponent<Animator>();

            if (driverAnimator == animator)
            {
                driver.enabled = false;
                Debug.LogWarning("MediaPipeHumanoidIK: Disabled conflicting MediaPipeHumanoidAvatar on " + driver.gameObject.name + ".");
            }
        }

        IKSelfTest[] ikTests = FindObjectsByType<IKSelfTest>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (IKSelfTest test in ikTests)
        {
            if (test == null || !test.enabled)
            {
                continue;
            }

            Animator testAnimator = test.animator != null ? test.animator : test.GetComponent<Animator>();

            if (testAnimator == animator)
            {
                test.enabled = false;
                Debug.LogWarning("MediaPipeHumanoidIK: Disabled conflicting IKSelfTest on " + test.gameObject.name + ".");
            }
        }
    }

    private void CacheBoneAimAxes()
    {
        leftUpperArmAimAxis = ComputeAimAxis(leftUpperArmBone, leftLowerArmBone, leftUpperArmAimAxis);
        rightUpperArmAimAxis = ComputeAimAxis(rightUpperArmBone, rightLowerArmBone, rightUpperArmAimAxis);

        leftLowerArmAimAxis = ComputeAimAxis(leftLowerArmBone, leftHandBone, leftLowerArmAimAxis);
        rightLowerArmAimAxis = ComputeAimAxis(rightLowerArmBone, rightHandBone, rightLowerArmAimAxis);

        leftUpperLegAimAxis = ComputeAimAxis(leftUpperLegBone, leftLowerLegBone, leftUpperLegAimAxis);
        rightUpperLegAimAxis = ComputeAimAxis(rightUpperLegBone, rightLowerLegBone, rightUpperLegAimAxis);

        leftLowerLegAimAxis = ComputeAimAxis(leftLowerLegBone, leftFootBone, leftLowerLegAimAxis);
        rightLowerLegAimAxis = ComputeAimAxis(rightLowerLegBone, rightFootBone, rightLowerLegAimAxis);
    }

    private void CacheRestLocalRotations()
    {
        headRestLocalRotation = GetLocalRotationOrIdentity(headBone);

        leftUpperArmRestLocalRotation = GetLocalRotationOrIdentity(leftUpperArmBone);
        leftLowerArmRestLocalRotation = GetLocalRotationOrIdentity(leftLowerArmBone);
        rightUpperArmRestLocalRotation = GetLocalRotationOrIdentity(rightUpperArmBone);
        rightLowerArmRestLocalRotation = GetLocalRotationOrIdentity(rightLowerArmBone);

        leftUpperLegRestLocalRotation = GetLocalRotationOrIdentity(leftUpperLegBone);
        leftLowerLegRestLocalRotation = GetLocalRotationOrIdentity(leftLowerLegBone);
        rightUpperLegRestLocalRotation = GetLocalRotationOrIdentity(rightUpperLegBone);
        rightLowerLegRestLocalRotation = GetLocalRotationOrIdentity(rightLowerLegBone);
    }

    private Quaternion GetLocalRotationOrIdentity(Transform bone)
    {
        if (bone == null)
        {
            return Quaternion.identity;
        }

        return bone.localRotation;
    }

    private Vector3 ComputeAimAxis(Transform bone, Transform childBone, Vector3 fallbackAxis)
    {
        if (bone == null || childBone == null)
        {
            return fallbackAxis.normalized;
        }

        Vector3 worldDirection = childBone.position - bone.position;

        if (worldDirection.sqrMagnitude < 0.000001f)
        {
            return fallbackAxis.normalized;
        }

        Vector3 localDirection = bone.InverseTransformDirection(worldDirection.normalized);

        if (localDirection.sqrMagnitude < 0.000001f)
        {
            return fallbackAxis.normalized;
        }

        return localDirection.normalized;
    }

    private void RotateBoneToward(
        Transform bone,
        Vector3 aimAxis,
        Vector3 direction,
        float weight,
        Vector3 axisOffset,
        Quaternion restLocalRotation
    )
    {
        if (bone == null || weight <= 0.0f)
        {
            return;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Transform parent = bone.parent;

        if (parent == null)
        {
            return;
        }

        Vector3 targetLocalDirection = Quaternion.Inverse(parent.rotation) * direction.normalized;

        if (targetLocalDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Quaternion alignLocal = Quaternion.FromToRotation(aimAxis.normalized, targetLocalDirection.normalized);
        Quaternion desiredLocalRotation = alignLocal * restLocalRotation * Quaternion.Euler(axisOffset);

        bone.localRotation = Quaternion.Slerp(
            bone.localRotation,
            desiredLocalRotation,
            Time.deltaTime * boneRotationSmoothing * weight
        );
    }

    private void OnAnimatorIK(int layerIndex)
    {
        lastIkCallbackTime = Time.time;

        if (!hasPose || animator == null)
        {
            return;
        }

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handWeight);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget);

        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
        animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget);

        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, elbowHintWeight);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftElbowHint);

        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, elbowHintWeight);
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint);

        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, footWeight);
        animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootTarget);

        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, footWeight);
        animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootTarget);

        animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, kneeHintWeight);
        animator.SetIKHintPosition(AvatarIKHint.LeftKnee, leftKneeHint);

        animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, kneeHintWeight);
        animator.SetIKHintPosition(AvatarIKHint.RightKnee, rightKneeHint);

        animator.SetLookAtWeight(lookWeight);
        animator.SetLookAtPosition(headTarget);

        if (!driveBoneRotations || latestPacket == null || latestPlayer == null)
        {
            return;
        }

        ApplyBoneRotations(latestPacket, latestPlayer);
    }

    private void ValidateBoneBindings()
    {
        bool missingBones =
            leftUpperArmBone == null ||
            leftLowerArmBone == null ||
            rightUpperArmBone == null ||
            rightLowerArmBone == null ||
            leftUpperLegBone == null ||
            leftLowerLegBone == null ||
            rightUpperLegBone == null ||
            rightLowerLegBone == null;

        if (missingBones)
        {
            warnedMissingBones = true;
            Debug.LogError(
                "MediaPipeHumanoidIK: Missing humanoid bone transforms. Check model Rig settings: Avatar must be Humanoid and, if Optimize Game Objects is enabled, expose required bones."
            );
        }
    }
}