using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class MiniRigVisualizer : MonoBehaviour
{
    [Header("Tracking")]
    public UdpPoseReceiver receiver;
    public int playerId = 1;

    [Header("Mapping")]
    public bool mirrorX = false;
    public float horizontalScale = 1.2f;
    public float verticalScale = 1.2f;
    public float horizontalMotionGain = 1.0f;
    public float verticalMotionGain = 1.0f;
    public float depthScale = 0.3f;
    public float depthMotionGain = 2.8f;
    public float forwardOffset = 0.4f;
    public float bodyYOffset = 1.0f;

    [Header("Rig Look")]
    public float jointSize = 0.06f;
    public float boneRadius = 0.03f;
    public float smoothing = 18.0f;
    public Color jointColor = new Color(0.98f, 0.86f, 0.1f, 1f);
    public Color boneColor = new Color(0.1f, 0.95f, 0.5f, 1f);

    private readonly Dictionary<string, Transform> jointTransforms = new Dictionary<string, Transform>();
    private readonly List<BoneVisual> boneVisuals = new List<BoneVisual>();
    private readonly Dictionary<string, Vector3> defaultPose = new Dictionary<string, Vector3>();

    private static readonly string[] JointNames =
    {
        "nose",
        "left_shoulder", "right_shoulder",
        "left_elbow", "right_elbow",
        "left_wrist", "right_wrist",
        "left_hip", "right_hip",
        "left_knee", "right_knee",
        "left_ankle", "right_ankle",
        "left_foot_index", "right_foot_index",
    };

    private static readonly BoneDef[] BoneDefs =
    {
        new BoneDef("shoulders", "left_shoulder", "right_shoulder"),
        new BoneDef("hips", "left_hip", "right_hip"),
        new BoneDef("left_torso", "left_shoulder", "left_hip"),
        new BoneDef("right_torso", "right_shoulder", "right_hip"),

        new BoneDef("left_upper_arm", "left_shoulder", "left_elbow"),
        new BoneDef("left_lower_arm", "left_elbow", "left_wrist"),
        new BoneDef("right_upper_arm", "right_shoulder", "right_elbow"),
        new BoneDef("right_lower_arm", "right_elbow", "right_wrist"),

        new BoneDef("left_upper_leg", "left_hip", "left_knee"),
        new BoneDef("left_lower_leg", "left_knee", "left_ankle"),
        new BoneDef("right_upper_leg", "right_hip", "right_knee"),
        new BoneDef("right_lower_leg", "right_knee", "right_ankle"),

        new BoneDef("left_foot", "left_ankle", "left_foot_index"),
        new BoneDef("right_foot", "right_ankle", "right_foot_index"),

        new BoneDef("head", "left_shoulder", "nose"),
    };

    private Material jointMaterial;
    private Material boneMaterial;

    private void OnEnable()
    {
        BuildDefaultPose();
        EnsureRig();
        ApplyDefaultPose();
    }

    private void OnValidate()
    {
        BuildDefaultPose();
        EnsureRig();
        ApplyDefaultPose();
    }

    private void Update()
    {
        EnsureRig();

        if (!Application.isPlaying)
        {
            ApplyDefaultPose();
            return;
        }

        if (receiver == null)
        {
            receiver = Object.FindFirstObjectByType<UdpPoseReceiver>();
            if (receiver == null)
            {
                ApplyDefaultPose();
                return;
            }
        }

        PosePacket packet = receiver.GetLatestPacket();
        PlayerPose player = receiver.GetPlayer(playerId);

        if (packet == null || packet.frame == null || player == null || player.joints == null)
        {
            ApplyDefaultPose();
            return;
        }

        Vector2 bodyCenter = GetBodyCenter(player);
        float bodySize = GetBodySize(player);
        float bodyDepthCenter = GetBodyDepthCenter(player);

        foreach (string jointName in JointNames)
        {
            Transform jointTransform;
            if (!jointTransforms.TryGetValue(jointName, out jointTransform))
            {
                continue;
            }

            JointPoint point = GetJoint(player.joints, jointName);
            if (point == null)
            {
                continue;
            }

            Vector3 target = JointToWorld(point, bodyCenter, bodySize, bodyDepthCenter);
            jointTransform.position = Vector3.Lerp(jointTransform.position, target, Time.deltaTime * smoothing);
        }

        UpdateBones();
    }

    private void EnsureRig()
    {
        EnsureMaterials();

        jointTransforms.Clear();

        foreach (string jointName in JointNames)
        {
            Transform jointTransform = transform.Find(jointName);

            if (jointTransform == null)
            {
                GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                joint.name = jointName;
                joint.transform.SetParent(transform, false);
                jointTransform = joint.transform;

                Collider collider = joint.GetComponent<Collider>();
                if (collider != null)
                {
                    SafeDestroy(collider);
                }
            }

            jointTransform.localScale = Vector3.one * jointSize;

            Renderer jointRenderer = jointTransform.GetComponent<Renderer>();
            if (jointRenderer != null)
            {
                jointRenderer.sharedMaterial = jointMaterial;
            }

            jointTransforms[jointName] = jointTransform;
        }

        boneVisuals.Clear();

        foreach (BoneDef boneDef in BoneDefs)
        {
            Transform boneTransform = transform.Find(boneDef.Name);

            if (boneTransform == null)
            {
                GameObject bone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bone.name = boneDef.Name;
                bone.transform.SetParent(transform, false);
                boneTransform = bone.transform;

                Collider collider = bone.GetComponent<Collider>();
                if (collider != null)
                {
                    SafeDestroy(collider);
                }
            }

            Renderer boneRenderer = boneTransform.GetComponent<Renderer>();
            if (boneRenderer != null)
            {
                boneRenderer.sharedMaterial = boneMaterial;
            }

            boneVisuals.Add(new BoneVisual
            {
                Start = boneDef.Start,
                End = boneDef.End,
                Transform = boneTransform,
            });
        }
    }

    private void EnsureMaterials()
    {
        if (jointMaterial == null)
        {
            jointMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        if (boneMaterial == null)
        {
            boneMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        jointMaterial.color = jointColor;
        boneMaterial.color = boneColor;
    }

    private void BuildDefaultPose()
    {
        defaultPose.Clear();

        defaultPose["nose"] = new Vector3(0.0f, 1.9f, 0.0f);

        defaultPose["left_shoulder"] = new Vector3(-0.23f, 1.65f, 0.0f);
        defaultPose["right_shoulder"] = new Vector3(0.23f, 1.65f, 0.0f);

        defaultPose["left_elbow"] = new Vector3(-0.45f, 1.5f, 0.0f);
        defaultPose["right_elbow"] = new Vector3(0.45f, 1.5f, 0.0f);

        defaultPose["left_wrist"] = new Vector3(-0.62f, 1.33f, 0.0f);
        defaultPose["right_wrist"] = new Vector3(0.62f, 1.33f, 0.0f);

        defaultPose["left_hip"] = new Vector3(-0.16f, 1.2f, 0.0f);
        defaultPose["right_hip"] = new Vector3(0.16f, 1.2f, 0.0f);

        defaultPose["left_knee"] = new Vector3(-0.16f, 0.82f, 0.0f);
        defaultPose["right_knee"] = new Vector3(0.16f, 0.82f, 0.0f);

        defaultPose["left_ankle"] = new Vector3(-0.16f, 0.45f, 0.0f);
        defaultPose["right_ankle"] = new Vector3(0.16f, 0.45f, 0.0f);

        defaultPose["left_foot_index"] = new Vector3(-0.16f, 0.37f, 0.18f);
        defaultPose["right_foot_index"] = new Vector3(0.16f, 0.37f, 0.18f);
    }

    private void ApplyDefaultPose()
    {
        foreach (KeyValuePair<string, Vector3> entry in defaultPose)
        {
            Transform jointTransform;
            if (!jointTransforms.TryGetValue(entry.Key, out jointTransform))
            {
                continue;
            }

            jointTransform.localPosition = entry.Value;
        }

        UpdateBones();
    }

    private void UpdateBones()
    {
        for (int i = 0; i < boneVisuals.Count; i++)
        {
            BoneVisual bone = boneVisuals[i];

            Transform start;
            Transform end;

            if (!jointTransforms.TryGetValue(bone.Start, out start) || !jointTransforms.TryGetValue(bone.End, out end))
            {
                continue;
            }

            Vector3 delta = end.position - start.position;
            float length = delta.magnitude;

            if (length < 0.0001f)
            {
                continue;
            }

            bone.Transform.position = (start.position + end.position) * 0.5f;
            bone.Transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            bone.Transform.localScale = new Vector3(boneRadius, length * 0.5f, boneRadius);
        }
    }

    private Vector2 GetBodyCenter(PlayerPose player)
    {
        JointCollection joints = player.joints;

        float x =
            joints.left_hip.x +
            joints.right_hip.x +
            joints.left_shoulder.x +
            joints.right_shoulder.x;

        float y =
            joints.left_hip.y +
            joints.right_hip.y +
            joints.left_shoulder.y +
            joints.right_shoulder.y;

        return new Vector2(x * 0.25f, y * 0.25f);
    }

    private float GetBodySize(PlayerPose player)
    {
        JointCollection joints = player.joints;

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

    private float GetBodyDepthCenter(PlayerPose player)
    {
        JointCollection joints = player.joints;

        return (
            joints.left_hip.z +
            joints.right_hip.z +
            joints.left_shoulder.z +
            joints.right_shoulder.z
        ) * 0.25f;
    }

    private Vector3 JointToWorld(JointPoint joint, Vector2 bodyCenter, float bodySize, float bodyDepthCenter)
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

    private static JointPoint GetJoint(JointCollection joints, string name)
    {
        switch (name)
        {
            case "nose": return joints.nose;
            case "left_shoulder": return joints.left_shoulder;
            case "right_shoulder": return joints.right_shoulder;
            case "left_elbow": return joints.left_elbow;
            case "right_elbow": return joints.right_elbow;
            case "left_wrist": return joints.left_wrist;
            case "right_wrist": return joints.right_wrist;
            case "left_hip": return joints.left_hip;
            case "right_hip": return joints.right_hip;
            case "left_knee": return joints.left_knee;
            case "right_knee": return joints.right_knee;
            case "left_ankle": return joints.left_ankle;
            case "right_ankle": return joints.right_ankle;
            case "left_foot_index": return joints.left_foot_index;
            case "right_foot_index": return joints.right_foot_index;
            default: return null;
        }
    }

    private static void SafeDestroy(Object obj)
    {
        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }

    private struct BoneVisual
    {
        public string Start;
        public string End;
        public Transform Transform;
    }

    private struct BoneDef
    {
        public string Name;
        public string Start;
        public string End;

        public BoneDef(string name, string start, string end)
        {
            Name = name;
            Start = start;
            End = end;
        }
    }
}
