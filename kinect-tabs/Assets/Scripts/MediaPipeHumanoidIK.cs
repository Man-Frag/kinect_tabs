using UnityEngine;

public class MediaPipeHumanoidIK : MonoBehaviour
{
    [Header("Tracking")]
    public UdpPoseReceiver receiver;
    public int playerId = 1;

    [Header("Animator")]
    public Animator animator;

    [Header("Mirror / Swap")]
    public bool swapLeftRight = true;
    public bool mirrorX = false;

    [Header("Body Mapping")]
    public float horizontalScale = 3.0f;
    public float verticalScale = 3.0f;
    public float depthScale = 1.0f;
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

        UpdateTarget(packet, leftWrist, bodyCenter, bodySize, ref leftHandTarget);
        UpdateTarget(packet, rightWrist, bodyCenter, bodySize, ref rightHandTarget);

        UpdateTarget(packet, leftElbow, bodyCenter, bodySize, ref leftElbowHint);
        UpdateTarget(packet, rightElbow, bodyCenter, bodySize, ref rightElbowHint);

        UpdateTarget(packet, leftAnkle, bodyCenter, bodySize, ref leftFootTarget);
        UpdateTarget(packet, rightAnkle, bodyCenter, bodySize, ref rightFootTarget);

        UpdateTarget(packet, leftKnee, bodyCenter, bodySize, ref leftKneeHint);
        UpdateTarget(packet, rightKnee, bodyCenter, bodySize, ref rightKneeHint);

        UpdateTarget(packet, player.joints.nose, bodyCenter, bodySize, ref headTarget);
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

    private void UpdateTarget(
        PosePacket packet,
        JointPoint joint,
        Vector2 bodyCenter,
        float bodySize,
        ref Vector3 target
    )
    {
        if (joint == null)
        {
            return;
        }

        Vector3 mappedPosition = JointToAvatarWorld(joint, bodyCenter, bodySize);

        target = Vector3.Lerp(
            target,
            mappedPosition,
            Time.deltaTime * targetSmoothing
        );
    }

    private Vector3 JointToAvatarWorld(JointPoint joint, Vector2 bodyCenter, float bodySize)
    {
        float relativeX = (joint.x - bodyCenter.x) / bodySize;
        float relativeY = -(joint.y - bodyCenter.y) / bodySize;

        if (mirrorX)
        {
            relativeX = -relativeX;
        }

        float relativeZ = -joint.z * depthScale;

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

    private void OnAnimatorIK(int layerIndex)
    {
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
    }
}