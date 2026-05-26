using UnityEngine;

public class SimpleFightGame : MonoBehaviour
{
    [Header("Tracking")]
    public UdpPoseReceiver receiver;
    public int playerId = 1;

    [Header("Optional Model")]
    public Transform playerModel;

    [Header("Scene Mapping")]
    public float worldWidth = 6.0f;
    public float worldHeight = 3.5f;
    public float pointZ = -0.3f;

    [Header("Model Movement")]
    public float modelY = 0.0f;
    public float modelZ = 0.0f;
    public float modelSmoothing = 12.0f;

    [Header("Attack Points")]
    public float attackSphereSize = 0.25f;
    public float attackPointSmoothing = 25.0f;

    [Header("Target")]
    public Vector3 targetPosition = new Vector3(2.0f, 1.0f, -0.3f);
    public float targetSize = 0.8f;
    public float hitDistance = 0.55f;

    [Header("Score")]
    public int winningScore = 10;
    public float hitCooldown = 0.7f;

    private Transform leftFist;
    private Transform rightFist;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform target;

    private int player1Score = 0;
    private bool gameOver = false;
    private float lastHitTime = -999f;

    private void Start()
    {
        leftFist = CreateSphere("Left Fist", Color.yellow, attackSphereSize);
        rightFist = CreateSphere("Right Fist", Color.yellow, attackSphereSize);
        leftFoot = CreateSphere("Left Foot", Color.green, attackSphereSize);
        rightFoot = CreateSphere("Right Foot", Color.green, attackSphereSize);

        target = CreateSphere("Enemy Target", Color.red, targetSize);
        target.position = targetPosition;

        Debug.Log("Simple fight game started. No colliders, no rigidbodies, no hitboxes.");
    }

    private Transform CreateSphere(string objectName, Color color, float size)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = objectName;

        sphere.transform.localScale = new Vector3(size, size, size);

        Renderer renderer = sphere.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = color;
        }

        Collider collider = sphere.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        return sphere.transform;
    }

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

        MovePlayerModel(packet, player);
        MoveAttackPoints(packet, player);
        CheckHits();
    }

    private void MovePlayerModel(PosePacket packet, PlayerPose player)
    {
        if (playerModel == null)
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

        Vector3 targetModelPosition = new Vector3(worldX, modelY, modelZ);

        playerModel.position = Vector3.Lerp(
            playerModel.position,
            targetModelPosition,
            Time.deltaTime * modelSmoothing
        );
    }

    private void MoveAttackPoints(PosePacket packet, PlayerPose player)
    {
        MovePoint(packet, player.joints.left_wrist, leftFist);
        MovePoint(packet, player.joints.right_wrist, rightFist);
        MovePoint(packet, player.joints.left_foot_index, leftFoot);
        MovePoint(packet, player.joints.right_foot_index, rightFoot);
    }

    private void MovePoint(PosePacket packet, JointPoint joint, Transform sphere)
    {
        if (joint == null || sphere == null)
        {
            return;
        }

        Vector3 targetPosition = CameraPointToWorld(packet, joint, pointZ);

        sphere.position = Vector3.Lerp(
            sphere.position,
            targetPosition,
            Time.deltaTime * attackPointSmoothing
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

    private void CheckHits()
    {
        if (gameOver)
        {
            return;
        }

        if (Time.time - lastHitTime < hitCooldown)
        {
            return;
        }

        if (IsCloseEnough(leftFist, target))
        {
            RegisterHit("Left Fist");
        }
        else if (IsCloseEnough(rightFist, target))
        {
            RegisterHit("Right Fist");
        }
        else if (IsCloseEnough(leftFoot, target))
        {
            RegisterHit("Left Foot");
        }
        else if (IsCloseEnough(rightFoot, target))
        {
            RegisterHit("Right Foot");
        }
    }

    private bool IsCloseEnough(Transform attacker, Transform defender)
    {
        if (attacker == null || defender == null)
        {
            return false;
        }

        float distance = Vector3.Distance(attacker.position, defender.position);

        return distance <= hitDistance;
    }

    private void RegisterHit(string attackName)
    {
        lastHitTime = Time.time;
        player1Score++;

        Debug.Log($"Hit with {attackName}. Score: {player1Score}");

        if (player1Score >= winningScore)
        {
            gameOver = true;
            Debug.Log("Player 1 wins!");
        }
    }
}