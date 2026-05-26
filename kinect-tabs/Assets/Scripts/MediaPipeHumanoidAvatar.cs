using UnityEngine;

public class MediaPipeHumanoidAvatar : MonoBehaviour
{
    [Header("Tracking")]
    public UdpPoseReceiver receiver;
    public int playerId = 1;

    [Header("Avatar")]
    public Animator animator;

    [Header("Root Movement")]
    public bool moveRoot = true;
    public float rootMoveScale = 6.0f;
    public float rootSmoothing = 10.0f;

    [Header("Rotation Settings")]
    public float smoothing = 18.0f;
    public float armWeight = 1.0f;
    public float legWeight = 1.0f;
    public float headWeight = 0.35f;

    [Header("Bone Axis Correction")]
    public Vector3 upperArmAxisOffset = new Vector3(0, 0, 0);
    public Vector3 lowerArmAxisOffset = new Vector3(0, 0, 0);
    public Vector3 upperLegAxisOffset = new Vector3(0, 0, 0);
    public Vector3 lowerLegAxisOffset = new Vector3(0, 0, 0);
    public Vector3 headAxisOffset = new Vector3(0, 0, 0);

    private Transform hips;
    private Transform head;

    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform rightUpperArm;
    private Transform rightLowerArm;

    private Transform leftUpperLeg;
    private Transform leftLowerLeg;
    private Transform rightUpperLeg;
    private Transform rightLowerLeg;

    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError("MediaPipeHumanoidAvatar: Animator is missing.");
            return;
        }

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        head = animator.GetBoneTransform(HumanBodyBones.Head);

        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);

        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

        leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);

        rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

        Debug.Log("MediaPipeHumanoidAvatar initialized.");
    }

    private void LateUpdate()
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

        if (moveRoot)
        {
            MoveAvatarRoot(packet, player);
        }

        RotateHead(player);
        RotateArms(player);
        RotateLegs(player);
    }

    private void MoveAvatarRoot(PosePacket packet, PlayerPose player)
    {
        JointPoint leftHip = player.joints.left_hip;
        JointPoint rightHip = player.joints.right_hip;

        if (leftHip == null || rightHip == null)
        {
            return;
        }

        float hipX = (leftHip.x + rightHip.x) * 0.5f;
        float normalizedX = hipX / packet.frame.width;
        float worldX = (normalizedX - 0.5f) * rootMoveScale;

        Vector3 targetPosition = new Vector3(worldX, transform.position.y, transform.position.z);

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * rootSmoothing
        );
    }

    private void RotateHead(PlayerPose player)
    {
        if (head == null)
        {
            return;
        }

        Vector3 leftShoulder = ToPoseVector(player.joints.left_shoulder);
        Vector3 rightShoulder = ToPoseVector(player.joints.right_shoulder);
        Vector3 nose = ToPoseVector(player.joints.nose);

        Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
        Vector3 direction = nose - shoulderCenter;

        ApplyRotation(head, direction, headWeight, headAxisOffset);
    }

    private void RotateArms(PlayerPose player)
    {
        Vector3 leftShoulder = ToPoseVector(player.joints.left_shoulder);
        Vector3 leftElbow = ToPoseVector(player.joints.left_elbow);
        Vector3 leftWrist = ToPoseVector(player.joints.left_wrist);

        Vector3 rightShoulder = ToPoseVector(player.joints.right_shoulder);
        Vector3 rightElbow = ToPoseVector(player.joints.right_elbow);
        Vector3 rightWrist = ToPoseVector(player.joints.right_wrist);

        ApplyRotation(leftUpperArm, leftElbow - leftShoulder, armWeight, upperArmAxisOffset);
        ApplyRotation(leftLowerArm, leftWrist - leftElbow, armWeight, lowerArmAxisOffset);

        ApplyRotation(rightUpperArm, rightElbow - rightShoulder, armWeight, upperArmAxisOffset);
        ApplyRotation(rightLowerArm, rightWrist - rightElbow, armWeight, lowerArmAxisOffset);
    }

    private void RotateLegs(PlayerPose player)
    {
        Vector3 leftHip = ToPoseVector(player.joints.left_hip);
        Vector3 leftKnee = ToPoseVector(player.joints.left_knee);
        Vector3 leftAnkle = ToPoseVector(player.joints.left_ankle);

        Vector3 rightHip = ToPoseVector(player.joints.right_hip);
        Vector3 rightKnee = ToPoseVector(player.joints.right_knee);
        Vector3 rightAnkle = ToPoseVector(player.joints.right_ankle);

        ApplyRotation(leftUpperLeg, leftKnee - leftHip, legWeight, upperLegAxisOffset);
        ApplyRotation(leftLowerLeg, leftAnkle - leftKnee, legWeight, lowerLegAxisOffset);

        ApplyRotation(rightUpperLeg, rightKnee - rightHip, legWeight, upperLegAxisOffset);
        ApplyRotation(rightLowerLeg, rightAnkle - rightKnee, legWeight, lowerLegAxisOffset);
    }

    private Vector3 ToPoseVector(JointPoint point)
    {
        if (point == null)
        {
            return Vector3.zero;
        }

        float x = point.x;
        float y = -point.y;
        float z = point.z * 1000.0f;

        return new Vector3(x, y, z);
    }

    private void ApplyRotation(Transform bone, Vector3 direction, float weight, Vector3 axisOffset)
    {
        if (bone == null)
        {
            return;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        Quaternion offsetRotation = Quaternion.Euler(axisOffset);
        Quaternion finalRotation = targetRotation * offsetRotation;

        bone.rotation = Quaternion.Slerp(
            bone.rotation,
            finalRotation,
            Time.deltaTime * smoothing * weight
        );
    }
}