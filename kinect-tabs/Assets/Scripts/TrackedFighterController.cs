using UnityEngine;

public class TrackedFighterController : MonoBehaviour
{
    public UdpPoseReceiver receiver;
    public int playerId = 1;

    public Transform modelRoot;

    public Transform leftFistHitbox;
    public Transform rightFistHitbox;
    public Transform leftFootHitbox;
    public Transform rightFootHitbox;

    public float worldWidth = 6.0f;
    public float worldHeight = 3.5f;

    public float modelY = 0.0f;
    public float modelZ = 0.0f;
    public float hitboxZ = -0.3f;

    public float modelSmoothing = 12.0f;
    public float hitboxSmoothing = 25.0f;

    private void Update()
    {
        if (receiver == null)
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

        UpdateModel(packet, player);

        UpdateHitbox(packet, player.joints.left_wrist, leftFistHitbox);
        UpdateHitbox(packet, player.joints.right_wrist, rightFistHitbox);
        UpdateHitbox(packet, player.joints.left_foot_index, leftFootHitbox);
        UpdateHitbox(packet, player.joints.right_foot_index, rightFootHitbox);
    }

    private void UpdateModel(PosePacket packet, PlayerPose player)
    {
        if (modelRoot == null)
        {
            return;
        }

        JointPoint leftHip = player.joints.left_hip;
        JointPoint rightHip = player.joints.right_hip;

        if (leftHip == null || rightHip == null)
        {
            return;
        }

        float hipX = (leftHip.x + rightHip.x) * 0.5f;
        float normalizedX = hipX / packet.frame.width;
        float worldX = (normalizedX - 0.5f) * worldWidth;

        Vector3 targetPosition = new Vector3(worldX, modelY, modelZ);

        modelRoot.position = Vector3.Lerp(
            modelRoot.position,
            targetPosition,
            Time.deltaTime * modelSmoothing
        );
    }

    private void UpdateHitbox(PosePacket packet, JointPoint joint, Transform hitbox)
    {
        if (joint == null || hitbox == null)
        {
            return;
        }

        Vector3 targetPosition = CameraPointToWorld(packet, joint, hitboxZ);

        hitbox.position = Vector3.Lerp(
            hitbox.position,
            targetPosition,
            Time.deltaTime * hitboxSmoothing
        );
    }

    private Vector3 CameraPointToWorld(PosePacket packet, JointPoint point, float z)
    {
        float normalizedX = point.x / packet.frame.width;
        float normalizedY = point.y / packet.frame.height;

        float worldX = (normalizedX - 0.5f) * worldWidth;
        float worldY = (0.5f - normalizedY) * worldHeight + 1.5f;

        return new Vector3(worldX, worldY, z);
    }
}