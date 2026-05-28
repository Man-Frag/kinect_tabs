using UnityEngine;

public class PipeTestBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBootstrapObjects()
    {
        UdpPoseReceiver receiver = Object.FindFirstObjectByType<UdpPoseReceiver>();

        if (receiver == null)
        {
            GameObject receiverObject = new GameObject("PoseReceiver");
            receiver = receiverObject.AddComponent<UdpPoseReceiver>();
            receiver.port = 5052;
        }

        MiniRigVisualizer rig = Object.FindFirstObjectByType<MiniRigVisualizer>();

        if (rig == null)
        {
            GameObject rigObject = new GameObject("MiniRig");
            rig = rigObject.AddComponent<MiniRigVisualizer>();
            rig.receiver = receiver;
        }

        Camera camera = Camera.main;

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.transform.position = new Vector3(0.0f, 1.45f, 4.0f);
        camera.transform.rotation = Quaternion.Euler(7.0f, 180.0f, 0.0f);

        Light light = Object.FindFirstObjectByType<Light>();

        if (light == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);
        }
    }
}
