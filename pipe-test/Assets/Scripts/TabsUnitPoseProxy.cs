using UnityEngine;

public class TabsUnitPoseProxy : MonoBehaviour
{
    [Header("Tracking")]
    public UdpPoseReceiver receiver;
    public int playerId = 1;

    [Header("Sources")]
    public MiniRigVisualizer miniRig;
    public Transform modelRoot;

    [Header("Motion")]
    public float positionSmoothing = 12.0f;
    public float rotationSmoothing = 10.0f;
    public float scaleSmoothing = 4.0f;

    [Header("Offsets")]
    public Vector3 modelPositionOffset = Vector3.zero;
    public Vector3 modelEulerOffset = Vector3.zero;

    private float baselineTorsoHeight = -1.0f;
    private Vector3 baseModelScale = Vector3.one;

    private void Start()
    {
        if (receiver == null)
        {
            receiver = Object.FindFirstObjectByType<UdpPoseReceiver>();
        }

        if (miniRig == null)
        {
            miniRig = Object.FindFirstObjectByType<MiniRigVisualizer>();
        }

        if (modelRoot == null)
        {
            modelRoot = transform;
        }

        baseModelScale = modelRoot.localScale;

        if (miniRig != null)
        {
            miniRig.showSkeleton = false;
        }

        Animator animator = modelRoot.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("TabsUnitPoseProxy: Model has no Animator. Using proxy motion only (no limb deformation).", this);
        }
    }

    private void Update()
    {
        if (receiver == null || modelRoot == null)
        {
            return;
        }

        PosePacket packet = receiver.GetLatestPacket();
        PlayerPose player = receiver.GetPlayer(playerId);

        if (packet == null || packet.frame == null || player == null || player.joints == null)
        {
            return;
        }

        JointCollection joints = player.joints;

        if (joints.left_shoulder == null || joints.right_shoulder == null || joints.left_hip == null || joints.right_hip == null)
        {
            return;
        }

        Vector2 bodyCenter = GetBodyCenter(joints);
        float bodySize = GetBodySize(joints);
        float bodyDepthCenter = GetBodyDepthCenter(joints);

        Vector3 leftHip = JointToWorld(joints.left_hip, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 rightHip = JointToWorld(joints.right_hip, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 leftShoulder = JointToWorld(joints.left_shoulder, bodyCenter, bodySize, bodyDepthCenter);
        Vector3 rightShoulder = JointToWorld(joints.right_shoulder, bodyCenter, bodySize, bodyDepthCenter);

        Vector3 hipsCenter = (leftHip + rightHip) * 0.5f;
        Vector3 shouldersCenter = (leftShoulder + rightShoulder) * 0.5f;

        Vector3 targetPosition = hipsCenter + modelPositionOffset;
        modelRoot.position = Vector3.Lerp(modelRoot.position, targetPosition, Time.deltaTime * positionSmoothing);

        Vector3 rightAxis = (rightShoulder - leftShoulder);
        rightAxis.y = 0.0f;

        if (rightAxis.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = Vector3.Cross(Vector3.up, rightAxis.normalized);

            if (forward.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(modelEulerOffset);
                modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, targetRotation, Time.deltaTime * rotationSmoothing);
            }
        }

        float torsoHeight = Vector3.Distance(shouldersCenter, hipsCenter);
        if (torsoHeight > 0.0001f && baselineTorsoHeight < 0.0f)
        {
            baselineTorsoHeight = torsoHeight;
        }

        if (baselineTorsoHeight > 0.0001f)
        {
            float scaleFactor = Mathf.Clamp(torsoHeight / baselineTorsoHeight, 0.65f, 1.5f);
            Vector3 targetScale = baseModelScale * scaleFactor;
            modelRoot.localScale = Vector3.Lerp(modelRoot.localScale, targetScale, Time.deltaTime * scaleSmoothing);
        }
    }

    private Vector2 GetBodyCenter(JointCollection joints)
    {
        float x = joints.left_hip.x + joints.right_hip.x + joints.left_shoulder.x + joints.right_shoulder.x;
        float y = joints.left_hip.y + joints.right_hip.y + joints.left_shoulder.y + joints.right_shoulder.y;
        return new Vector2(x * 0.25f, y * 0.25f);
    }

    private float GetBodySize(JointCollection joints)
    {
        Vector2 leftShoulder = new Vector2(joints.left_shoulder.x, joints.left_shoulder.y);
        Vector2 rightShoulder = new Vector2(joints.right_shoulder.x, joints.right_shoulder.y);
        Vector2 leftHip = new Vector2(joints.left_hip.x, joints.left_hip.y);
        Vector2 rightHip = new Vector2(joints.right_hip.x, joints.right_hip.y);

        Vector2 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
        Vector2 hipCenter = (leftHip + rightHip) * 0.5f;

        float torsoHeight = Vector2.Distance(shoulderCenter, hipCenter);
        float shoulderWidth = Vector2.Distance(leftShoulder, rightShoulder);

        return Mathf.Max(torsoHeight, shoulderWidth, 1.0f);
    }

    private float GetBodyDepthCenter(JointCollection joints)
    {
        return (joints.left_hip.z + joints.right_hip.z + joints.left_shoulder.z + joints.right_shoulder.z) * 0.25f;
    }

    private Vector3 JointToWorld(JointPoint joint, Vector2 bodyCenter, float bodySize, float bodyDepthCenter)
    {
        if (miniRig == null)
        {
            return modelRoot.position;
        }

        float relativeX = ((joint.x - bodyCenter.x) / bodySize) * miniRig.horizontalMotionGain;
        float relativeY = (-(joint.y - bodyCenter.y) / bodySize) * miniRig.verticalMotionGain;

        if (miniRig.mirrorX)
        {
            relativeX = -relativeX;
        }

        float relativeZ = -(joint.z - bodyDepthCenter) * miniRig.depthScale * miniRig.depthMotionGain;

        Vector3 localTarget = new Vector3(
            relativeX * miniRig.horizontalScale,
            relativeY * miniRig.verticalScale + miniRig.bodyYOffset,
            relativeZ + miniRig.forwardOffset
        );

        return miniRig.transform.TransformPoint(localTarget);
    }
}
