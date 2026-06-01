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

    private static void OnActiveSceneChangedInEditMode(
        UnityEngine.SceneManagement.Scene _,
        UnityEngine.SceneManagement.Scene __)
    {
        TryAutoCreateInEditMode();
    }

    private static void TryAutoCreateInEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        CreateSceneObjectsInternal(logIfUnchanged: false);
    }

    [MenuItem("Tools/Pipe Test/Setup Scene")]
    public static void SetupScene()
    {
        CreateSceneObjectsInternal(logIfUnchanged: true);
    }

    // Keep the old menu entry pointing at the same implementation so
    // existing muscle memory still works.
    [MenuItem("Tools/Pipe Test/Create Mini Rig Scene Objects")]
    public static void CreateMiniRigSceneObjects()
    {
        CreateSceneObjectsInternal(logIfUnchanged: true);
    }

    // -------------------------------------------------------------------------

    private static void CreateSceneObjectsInternal(bool logIfUnchanged)
    {
        bool changed = false;

        // ---- Pose Receiver ----
        UdpPoseReceiver receiver = Object.FindFirstObjectByType<UdpPoseReceiver>();
        if (receiver == null)
        {
            GameObject go = new GameObject("PoseReceiver");
            receiver = go.AddComponent<UdpPoseReceiver>();
            receiver.port = 5052;
            changed = true;
        }

        // ---- MiniRig (calibration + coordinate conversion; may be invisible) ----
        MiniRigVisualizer rig = Object.FindFirstObjectByType<MiniRigVisualizer>();
        if (rig == null)
        {
            GameObject go = new GameObject("MiniRig");
            rig = go.AddComponent<MiniRigVisualizer>();
            changed = true;
        }
        rig.receiver = receiver;

        // ---- TABS Unit Player ----
        bool tabsReady = SetupTabsUnitPlayer(receiver, rig, ref changed);

        // Hide the wire skeleton when the model is driving visuals; show it
        // for debugging when the model isn't available yet.
        bool wantSkeleton = !tabsReady;
        if (rig.showSkeleton != wantSkeleton)
        {
            rig.showSkeleton = wantSkeleton;
            changed = true;
        }

        // ---- Camera ----
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            camera = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            changed = true;
        }
        camera.transform.position = new Vector3(0.0f, 1.45f, 4.0f);
        camera.transform.rotation = Quaternion.Euler(7.0f, 180.0f, 0.0f);

        // ---- Directional Light ----
        Light directional = Object.FindFirstObjectByType<Light>();
        if (directional == null)
        {
            GameObject go = new GameObject("Directional Light");
            directional = go.AddComponent<Light>();
            directional.type = LightType.Directional;
            changed = true;
        }
        directional.intensity = 1.6f;
        directional.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            string msg = tabsReady
                ? "Pipe Test: scene set up with TabsUnit model."
                : "Pipe Test: scene set up (wire skeleton). " +
                  "Copy Tabs_unit.fbx to Assets/Models and re-run Setup Scene " +
                  "to add the model.";
            Debug.Log(msg);
        }
        else if (logIfUnchanged)
        {
            Debug.Log("Pipe Test: scene is already up to date.");
        }
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds or creates the "TabsUnitPlayer" GameObject from the imported
    /// <c>Tabs_unit.fbx</c>, attaches <see cref="TabsUnitRigDriver"/>, and
    /// wires up its references.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the player is present and correctly wired up.
    /// </returns>
    private static bool SetupTabsUnitPlayer(
        UdpPoseReceiver receiver,
        MiniRigVisualizer rig,
        ref bool changed)
    {
        const string playerName = "TabsUnitPlayer";
        const string fbxPath    = "Assets/Models/Tabs_unit.fbx";

        GameObject playerObject = GameObject.Find(playerName);

        if (playerObject == null)
        {
            // Try to load the imported FBX asset.
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelAsset == null)
            {
                // Not imported yet — caller will fall back to skeleton mode.
                return false;
            }

            // Instantiate as a prefab (keeps the link to the source asset).
            playerObject = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (playerObject == null)
                playerObject = Object.Instantiate(modelAsset);

            playerObject.name = playerName;
            changed = true;
        }

        if (!playerObject.activeSelf)
        {
            playerObject.SetActive(true);
            changed = true;
        }

        // Remove the old pose proxy if it's still on the object; TabsUnitRigDriver
        // supersedes it (handles both root motion and bone driving).
        TabsUnitPoseProxy proxy = playerObject.GetComponent<TabsUnitPoseProxy>();
        if (proxy != null)
        {
            Object.DestroyImmediate(proxy);
            changed = true;
        }

        // Ensure TabsUnitRigDriver is on the root of the player object.
        TabsUnitRigDriver driver = playerObject.GetComponent<TabsUnitRigDriver>();
        if (driver == null)
        {
            driver = playerObject.AddComponent<TabsUnitRigDriver>();
            changed = true;
        }

        if (driver.receiver != receiver) { driver.receiver = receiver; changed = true; }
        if (driver.miniRig  != rig)      { driver.miniRig  = rig;      changed = true; }

        // Sanity-check: the mixamorig:Spine bone must be findable in the hierarchy.
        // (Generic rig — no HumanBodyBones mapping needed.)
        bool spineFound = false;
        foreach (Transform t in playerObject.GetComponentsInChildren<Transform>())
        {
            if (t.name == "mixamorig:Spine") { spineFound = true; break; }
        }
        if (!spineFound)
        {
            Debug.LogWarning(
                "Pipe Test: 'mixamorig:Spine' not found under TabsUnitPlayer. " +
                "Check that Tabs_unit.fbx imported correctly " +
                "(right-click it in the Project window → Reimport).", playerObject);
        }

        return true;
    }
}
#endif
