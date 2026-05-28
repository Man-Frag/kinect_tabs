#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PipeTestSceneSetup
{
    [InitializeOnLoadMethod]
    private static void AutoSetupInEditMode()
    {
        EditorApplication.delayCall += TryAutoCreateInEditMode;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChangedInEditMode;
    }

    private static void OnActiveSceneChangedInEditMode(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
    {
        TryAutoCreateInEditMode();
    }

    private static void TryAutoCreateInEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        CreateMiniRigSceneObjectsInternal(logIfUnchanged: false);
    }

    [MenuItem("Tools/Pipe Test/Create Mini Rig Scene Objects")]
    public static void CreateMiniRigSceneObjects()
    {
        CreateMiniRigSceneObjectsInternal(logIfUnchanged: true);
    }

    private static void CreateMiniRigSceneObjectsInternal(bool logIfUnchanged)
    {
        bool createdSomething = false;

        UdpPoseReceiver receiver = Object.FindFirstObjectByType<UdpPoseReceiver>();

        if (receiver == null)
        {
            GameObject receiverObject = new GameObject("PoseReceiver");
            receiver = receiverObject.AddComponent<UdpPoseReceiver>();
            receiver.port = 5052;
            createdSomething = true;
        }

        MiniRigVisualizer rig = Object.FindFirstObjectByType<MiniRigVisualizer>();

        if (rig == null)
        {
            GameObject rigObject = new GameObject("MiniRig");
            rig = rigObject.AddComponent<MiniRigVisualizer>();
            createdSomething = true;
        }

        rig.receiver = receiver;

        Camera camera = Camera.main;

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            createdSomething = true;
        }

        camera.transform.position = new Vector3(0.0f, 1.45f, 4.0f);
        camera.transform.rotation = Quaternion.Euler(7.0f, 180.0f, 0.0f);

        Light directional = Object.FindFirstObjectByType<Light>();

        if (directional == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            createdSomething = true;
        }

        directional.intensity = 1.6f;
        directional.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }

        if (createdSomething)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Pipe Test: Mini rig scene objects created for edit mode visibility.");
        }
        else if (logIfUnchanged)
        {
            Debug.Log("Pipe Test: Mini rig scene objects already exist.");
        }
    }
}
#endif
