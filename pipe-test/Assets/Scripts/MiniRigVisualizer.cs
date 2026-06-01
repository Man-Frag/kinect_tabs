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

    [Header("Calibration")]
    public bool useCalibration = true;
    public KeyCode startCalibrationKey = KeyCode.C;
    public KeyCode capturePoseKey = KeyCode.Space;
    public KeyCode alternateCapturePoseKey = KeyCode.Return;
    public bool requirePoseValidation = false;
    public float calibrationCountdownSeconds = 5.0f;
    public bool enableCalibrationSound = true;
    public float countdownToneHz = 880.0f;
    public float phaseToneHz = 1320.0f;
    public float toneDurationSeconds = 0.08f;
    public float toneVolume = 0.2f;

    [Header("Rig Look")]
    public bool showSkeleton = true;
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

    private GUIStyle calibrationHeaderStyle;
    private GUIStyle calibrationBodyStyle;
    private GUIStyle calibrationBadgeStyle;

    private const int CalibrationPoseCount = 5;
    private readonly CalibrationSnapshot[] calibrationSnapshots = new CalibrationSnapshot[CalibrationPoseCount];

    // PlayerPrefs keys for persisting calibration gains across sessions.
    private const string PrefKey_HasCalib = "MiniRig_HasCalib";
    private const string PrefKey_HGain    = "MiniRig_HGain";
    private const string PrefKey_VGain    = "MiniRig_VGain";
    private const string PrefKey_ZGain    = "MiniRig_ZGain";

    private bool calibrationComplete;
    private bool calibrationRunning;
    private bool autoCalibrationMode;
    private int calibrationStage = -1;
    private float calibrationCountdownRemaining;
    private int countdownLastTick = -1;

    private AudioSource calibrationAudioSource;

    private static readonly string[] CalibrationPoseNames =
    {
        "Pose 1/5: stand straight, arms and legs closed.",
        "Pose 2/5: arms straight forward toward camera (like T-pose but hands pointing at lens), legs closed.",
        "Pose 3/5: T-pose with legs closed.",
        "Pose 4/5: T-pose with legs open (A-stance lower body).",
        "Pose 5/5: legs open, arms straight all the way up.",
    };

    private void OnEnable()
    {
        calibrationComplete = !useCalibration;
        EnsureCalibrationAudioSource();
        BuildDefaultPose();
        EnsureRig();
        ApplyDefaultPose();
    }

    // Start() is only called in Play mode (even with [ExecuteAlways]), so it's
    // the right place to restore persisted calibration without affecting the editor.
    private void Start()
    {
        LoadSavedCalibration();
    }

    private void OnValidate()
    {
        if (!useCalibration)
        {
            calibrationComplete = true;
            calibrationRunning = false;
            autoCalibrationMode = false;
            calibrationStage = -1;
        }

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

        // Drain UDP key events (from recording playback or live Python keystrokes)
        // before processing physical keys, so both sources drive calibration
        // identically and recordings replay correctly.
        KeyPacket keyEvent;
        while (receiver.TryDequeueKeyEvent(out keyEvent))
        {
            if (string.IsNullOrEmpty(keyEvent.@event) || keyEvent.@event == "keydown")
            {
                HandleUdpKeyDown(keyEvent.key, player);
            }
        }

        HandleCalibrationInput(player);

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

    private void HandleCalibrationInput(PlayerPose player)
    {
        if (!useCalibration || player == null || player.joints == null)
        {
            return;
        }

        if (!calibrationRunning && Input.GetKeyDown(startCalibrationKey))
        {
            StartCalibration();
            Debug.Log("MiniRig calibration started in MANUAL mode. " + CalibrationPoseNames[0]);
            return;
        }

        if (calibrationRunning)
        {
            if (Input.GetKeyDown(startCalibrationKey) && !autoCalibrationMode)
            {
                autoCalibrationMode = true;
                ResetAutoCountdown();
                PlayPhaseTone();
                Debug.Log("MiniRig calibration switched to AUTO mode.");
                return;
            }

            if (autoCalibrationMode)
            {
                UpdateCalibrationCountdown(player);
                return;
            }

            if (Input.GetKeyDown(capturePoseKey) || Input.GetKeyDown(alternateCapturePoseKey))
            {
                TryCaptureCalibrationPose(player);
            }
        }
    }

    /// <summary>
    /// Handles a single keydown received over UDP.  Mirrors the same logic as
    /// <see cref="HandleCalibrationInput"/> so that recording playback and live
    /// Python keystrokes drive calibration identically to physical key presses.
    /// </summary>
    private void HandleUdpKeyDown(string key, PlayerPose player)
    {
        if (!useCalibration || player == null || player.joints == null)
        {
            return;
        }

        if (!calibrationRunning && UdpKeyMatchesKeyCode(key, startCalibrationKey))
        {
            StartCalibration();
            Debug.Log("MiniRig calibration started via UDP key. " + CalibrationPoseNames[0]);
            return;
        }

        if (calibrationRunning)
        {
            if (UdpKeyMatchesKeyCode(key, startCalibrationKey) && !autoCalibrationMode)
            {
                autoCalibrationMode = true;
                ResetAutoCountdown();
                PlayPhaseTone();
                Debug.Log("MiniRig calibration switched to AUTO via UDP key.");
                return;
            }

            // Auto mode advances on its own timer; manual mode captures on
            // Space / Return, same as physical keys.
            if (!autoCalibrationMode &&
                (UdpKeyMatchesKeyCode(key, capturePoseKey) || UdpKeyMatchesKeyCode(key, alternateCapturePoseKey)))
            {
                TryCaptureCalibrationPose(player);
            }
        }
    }

    /// <summary>
    /// Returns true when the UDP key name (e.g. "c", "space", "return")
    /// matches the given Unity KeyCode, case-insensitively.
    /// Unity's KeyCode.ToString() produces names like "C", "Space", "Return"
    /// which match the names emitted by the Python _key_name() helper.
    /// </summary>
    private static bool UdpKeyMatchesKeyCode(string udpKey, KeyCode keyCode)
    {
        if (string.IsNullOrEmpty(udpKey))
        {
            return false;
        }

        return string.Equals(udpKey, keyCode.ToString(), System.StringComparison.OrdinalIgnoreCase);
    }

    private void StartCalibration()
    {
        for (int i = 0; i < calibrationSnapshots.Length; i++)
        {
            calibrationSnapshots[i] = default;
        }

        calibrationRunning = true;
        calibrationComplete = false;
        autoCalibrationMode = false;
        calibrationStage = 0;
        ResetAutoCountdown();
    }

    private void ResetAutoCountdown()
    {
        calibrationCountdownRemaining = Mathf.Max(1.0f, calibrationCountdownSeconds);
        countdownLastTick = Mathf.CeilToInt(calibrationCountdownRemaining) + 1;
    }

    private void UpdateCalibrationCountdown(PlayerPose player)
    {
        if (calibrationStage < 0 || calibrationStage >= CalibrationPoseCount)
        {
            return;
        }

        calibrationCountdownRemaining -= Time.deltaTime;

        int currentTick = Mathf.CeilToInt(Mathf.Max(0.0f, calibrationCountdownRemaining));

        if (currentTick > 0 && currentTick != countdownLastTick)
        {
            PlayCountdownTone();
            countdownLastTick = currentTick;
        }

        if (calibrationCountdownRemaining > 0.0f)
        {
            return;
        }

        PlayPhaseTone();

        bool captured = TryCaptureCalibrationPose(player);

        if (!captured)
        {
            ResetAutoCountdown();
        }
    }

    private bool TryCaptureCalibrationPose(PlayerPose player)
    {
        if (calibrationStage < 0 || calibrationStage >= CalibrationPoseCount)
        {
            return false;
        }

        bool poseIsValid = ValidateCalibrationPose(player, calibrationStage);

        if (requirePoseValidation && !poseIsValid)
        {
            Debug.LogWarning("MiniRig calibration pose check failed. Please match: " + CalibrationPoseNames[calibrationStage]);
            return false;
        }

        if (!poseIsValid)
        {
            Debug.LogWarning(
                "MiniRig calibration captured without strict pose validation at step " + (calibrationStage + 1) +
                ". Enable requirePoseValidation if you want hard checks."
            );
        }

        calibrationSnapshots[calibrationStage] = BuildSnapshot(player);
        calibrationStage++;

        if (calibrationStage >= CalibrationPoseCount)
        {
            FinishCalibration();
            return true;
        }

        if (autoCalibrationMode)
        {
            ResetAutoCountdown();
        }

        PlayPhaseTone();
        Debug.Log("MiniRig calibration captured. Next: " + CalibrationPoseNames[calibrationStage]);
        return true;
    }

    private void FinishCalibration()
    {
        // Snapshot indices after the 5-pose sequence:
        //   0 = closed pose (reference, not used for gain computation)
        //   1 = arms forward  → depth (Z) gain
        //   2 = T-pose closed → horizontal gain (arm span)
        //   3 = T-pose open   → horizontal gain (leg spread)
        //   4 = arms up open  → vertical gain
        CalibrationSnapshot armsForward    = calibrationSnapshots[1];
        CalibrationSnapshot tPoseClosed    = calibrationSnapshots[2];
        CalibrationSnapshot tPoseOpenLegs  = calibrationSnapshots[3];
        CalibrationSnapshot armsUpOpenLegs = calibrationSnapshots[4];

        // --- Horizontal gain (unchanged) ---
        float armSpanNorm   = SafeRatio(tPoseClosed.WristDistance,   tPoseClosed.TorsoHeight);
        float legSpreadNorm = SafeRatio(tPoseOpenLegs.AnkleDistance,  tPoseOpenLegs.TorsoHeight);
        const float targetArmSpanNorm  = 1.75f;
        const float targetLegSpreadNorm = 0.85f;
        float armBasedHorizontal = SafeRatio(targetArmSpanNorm,  armSpanNorm);
        float legBasedHorizontal = SafeRatio(targetLegSpreadNorm, legSpreadNorm);
        horizontalMotionGain = Mathf.Clamp((armBasedHorizontal + legBasedHorizontal) * 0.5f, 0.5f, 3.0f);

        // --- Vertical gain (unchanged) ---
        float armLiftNorm = SafeRatio(
            armsUpOpenLegs.ShoulderCenterY - armsUpOpenLegs.WristCenterY,
            armsUpOpenLegs.TorsoHeight);
        const float targetArmLiftNorm = 0.85f;
        verticalMotionGain = Mathf.Clamp(SafeRatio(targetArmLiftNorm, armLiftNorm), 0.5f, 3.0f);

        // --- Depth (Z) gain — new ---
        // Physical arm length is the same whether extending sideways (T-pose) or
        // forward (arms-forward pose).  We want equal avatar-space magnitude for
        // both, so we equate the two contributions:
        //
        //   relativeX (full arm side)    = (armLen_px / torsoHeight) * horizontalMotionGain
        //   relativeZ (full arm forward) = armForwardZ * depthScale  * depthMotionGain
        //
        // Setting relativeZ = relativeX and solving for depthMotionGain:
        //   depthMotionGain = (armLen_px / torsoHeight * horizontalMotionGain)
        //                     / (armForwardZ * depthScale)
        //
        // armForwardZ = BodyDepthCenter − WristCenterZ
        //   (positive when wrists are in front of the body; MediaPipe z is
        //    more negative the closer a point is to the camera)
        float armLengthPixels = tPoseClosed.WristDistance * 0.5f;
        float armLengthNorm   = SafeRatio(armLengthPixels, tPoseClosed.TorsoHeight);
        float armForwardZ     = armsForward.BodyDepthCenter - armsForward.WristCenterZ;

        if (armForwardZ > 0.0001f)
        {
            float targetRelativeX = armLengthNorm * horizontalMotionGain;
            depthMotionGain = Mathf.Clamp(
                SafeRatio(targetRelativeX, armForwardZ * depthScale),
                0.5f, 10.0f
            );
        }
        else
        {
            Debug.LogWarning(
                "MiniRig Z calibration: arms-forward pose did not produce a usable depth signal " +
                "(armForwardZ = " + armForwardZ.ToString("F4") + "). depthMotionGain unchanged."
            );
        }

        calibrationRunning = false;
        calibrationComplete = true;
        autoCalibrationMode = false;
        calibrationStage = -1;
        calibrationCountdownRemaining = 0.0f;
        countdownLastTick = -1;

        PlayPhaseTone();
        SaveCalibration();

        Debug.Log(
            "MiniRig calibration complete. Gains -> " +
            "X: " + horizontalMotionGain.ToString("F2") +
            ", Y: " + verticalMotionGain.ToString("F2") +
            ", Z: " + depthMotionGain.ToString("F2")
        );
    }

    /// <summary>Persists the three motion-gain values to PlayerPrefs.</summary>
    private void SaveCalibration()
    {
        PlayerPrefs.SetInt(PrefKey_HasCalib, 1);
        PlayerPrefs.SetFloat(PrefKey_HGain, horizontalMotionGain);
        PlayerPrefs.SetFloat(PrefKey_VGain, verticalMotionGain);
        PlayerPrefs.SetFloat(PrefKey_ZGain, depthMotionGain);
        PlayerPrefs.Save();   // flush to disk immediately

        Debug.Log(
            "MiniRig: calibration saved. Gains -> " +
            "X: " + horizontalMotionGain.ToString("F2") +
            ", Y: " + verticalMotionGain.ToString("F2") +
            ", Z: " + depthMotionGain.ToString("F2")
        );
    }

    /// <summary>
    /// Restores previously saved calibration gains from PlayerPrefs.
    /// Called from Start() so it only runs in Play mode.
    /// If no saved data exists, or useCalibration is off, does nothing.
    /// </summary>
    private void LoadSavedCalibration()
    {
        if (!useCalibration)
            return;

        if (PlayerPrefs.GetInt(PrefKey_HasCalib, 0) != 1)
            return;

        horizontalMotionGain = PlayerPrefs.GetFloat(PrefKey_HGain, horizontalMotionGain);
        verticalMotionGain   = PlayerPrefs.GetFloat(PrefKey_VGain, verticalMotionGain);
        depthMotionGain      = PlayerPrefs.GetFloat(PrefKey_ZGain, depthMotionGain);

        calibrationComplete = true;
        calibrationRunning  = false;

        Debug.Log(
            "MiniRig: loaded saved calibration. Gains -> " +
            "X: " + horizontalMotionGain.ToString("F2") +
            ", Y: " + verticalMotionGain.ToString("F2") +
            ", Z: " + depthMotionGain.ToString("F2")
        );
    }

    private static float SafeRatio(float numerator, float denominator)
    {
        if (Mathf.Abs(denominator) < 0.0001f)
        {
            return 1.0f;
        }

        return numerator / denominator;
    }

    private CalibrationSnapshot BuildSnapshot(PlayerPose player)
    {
        JointCollection j = player.joints;

        Vector2 leftShoulder = new Vector2(j.left_shoulder.x, j.left_shoulder.y);
        Vector2 rightShoulder = new Vector2(j.right_shoulder.x, j.right_shoulder.y);
        Vector2 leftHip = new Vector2(j.left_hip.x, j.left_hip.y);
        Vector2 rightHip = new Vector2(j.right_hip.x, j.right_hip.y);

        Vector2 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
        Vector2 hipCenter = (leftHip + rightHip) * 0.5f;
        float torsoHeight = Mathf.Max(Vector2.Distance(shoulderCenter, hipCenter), 0.0001f);

        Vector2 leftWrist = new Vector2(j.left_wrist.x, j.left_wrist.y);
        Vector2 rightWrist = new Vector2(j.right_wrist.x, j.right_wrist.y);
        Vector2 leftAnkle = new Vector2(j.left_ankle.x, j.left_ankle.y);
        Vector2 rightAnkle = new Vector2(j.right_ankle.x, j.right_ankle.y);

        // Depth centre of the torso (average z of shoulders + hips).
        // MediaPipe z is in the same normalised scale as landmark.x (not pixel-scaled),
        // and is negative when a point is closer to the camera.
        float bodyDepthCenter = (
            j.left_shoulder.z + j.right_shoulder.z +
            j.left_hip.z      + j.right_hip.z
        ) * 0.25f;

        return new CalibrationSnapshot
        {
            TorsoHeight = torsoHeight,
            WristDistance = Vector2.Distance(leftWrist, rightWrist),
            AnkleDistance = Vector2.Distance(leftAnkle, rightAnkle),
            ShoulderCenterY = shoulderCenter.y,
            WristCenterY = (leftWrist.y + rightWrist.y) * 0.5f,
            LeftWristY = leftWrist.y,
            RightWristY = rightWrist.y,
            LeftShoulderY = leftShoulder.y,
            RightShoulderY = rightShoulder.y,
            NoseY = j.nose != null ? j.nose.y : shoulderCenter.y,
            BodyDepthCenter = bodyDepthCenter,
            WristCenterZ = (j.left_wrist.z + j.right_wrist.z) * 0.5f,
        };
    }

    private bool ValidateCalibrationPose(PlayerPose player, int stage)
    {
        CalibrationSnapshot s = BuildSnapshot(player);

        float wristSpanNorm = SafeRatio(s.WristDistance, s.TorsoHeight);
        float ankleSpanNorm = SafeRatio(s.AnkleDistance, s.TorsoHeight);
        float wristsShoulderYOffset = Mathf.Abs(s.WristCenterY - s.ShoulderCenterY) / s.TorsoHeight;
        float wristsAboveShoulders = SafeRatio(s.ShoulderCenterY - s.WristCenterY, s.TorsoHeight);

        switch (stage)
        {
            case 0: // arms & legs closed
                return wristSpanNorm < 0.65f && ankleSpanNorm < 0.35f;

            case 1: // arms straight forward, legs closed
                // In 2D the wrists appear near shoulder height (not at sides or above).
                // They must also be clearly in front of the body in z.
                return wristsShoulderYOffset < 0.30f
                    && ankleSpanNorm < 0.45f
                    && (s.BodyDepthCenter - s.WristCenterZ) > 0.02f;

            case 2: // T-pose, legs closed (was case 1)
                return wristSpanNorm > 1.30f && ankleSpanNorm < 0.45f && wristsShoulderYOffset < 0.35f;

            case 3: // T-pose, legs open (was case 2)
                return wristSpanNorm > 1.30f && ankleSpanNorm > 0.60f && wristsShoulderYOffset < 0.35f;

            case 4: // arms up, legs open (was case 3)
                return ankleSpanNorm > 0.60f && wristsAboveShoulders > 0.55f && s.WristCenterY < s.NoseY;

            default:
                return false;
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !useCalibration)
        {
            return;
        }

        EnsureCalibrationGuiStyles();

        float panelWidth = Mathf.Min(Screen.width - 24f, 1220f);
        Rect panelRect = new Rect(12f, 12f, panelWidth, 130f);
        Rect headerRect = new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, 44f);
        Rect bodyRect = new Rect(panelRect.x + 12f, panelRect.y + 58f, panelRect.width - 24f, 62f);
        Rect badgeRect = new Rect(panelRect.x + panelRect.width - 220f, panelRect.y + 8f, 200f, 34f);

        string header;
        string body;
        Color stageColor;
        string badge;

        if (calibrationRunning && calibrationStage >= 0 && calibrationStage < CalibrationPoseNames.Length)
        {
            int countdownValue = Mathf.Max(1, Mathf.CeilToInt(calibrationCountdownRemaining));
            string countdownText = calibrationCountdownRemaining > 0.0f ? countdownValue.ToString() : "GO";

            header = "CALIBRATION RUNNING";

            if (autoCalibrationMode)
            {
                body = CalibrationPoseNames[calibrationStage] +
                       "  AUTO countdown: " + countdownText;
            }
            else
            {
                body = CalibrationPoseNames[calibrationStage] +
                       "  Capture: " + capturePoseKey + " / " + alternateCapturePoseKey +
                       "   Press " + startCalibrationKey + " again for AUTO 5..GO.";
            }

            stageColor = GetCalibrationStageColor(calibrationStage);
            badge = autoCalibrationMode
                ? "STEP " + (calibrationStage + 1) + " / " + CalibrationPoseCount + "  |  AUTO " + countdownText
                : "STEP " + (calibrationStage + 1) + " / " + CalibrationPoseCount + "  |  MANUAL";
        }
        else if (!calibrationComplete)
        {
            header = "CALIBRATION PENDING";
            body = "Press " + startCalibrationKey + " to start MANUAL calibration. During calibration, press " +
                   startCalibrationKey + " again to switch to AUTO countdown.";
            stageColor = new Color(1.0f, 0.64f, 0.0f, 0.95f);
            badge = "READY";
        }
        else
        {
            header = "CALIBRATION COMPLETE";
            body = "Gains  X=" + horizontalMotionGain.ToString("F2") +
                   "  Y=" + verticalMotionGain.ToString("F2") +
                   "  Z=" + depthMotionGain.ToString("F2") +
                   "   Press " + startCalibrationKey + " to recalibrate.";
            stageColor = new Color(0.17f, 0.75f, 0.27f, 0.95f);
            badge = "LOCKED";
        }

        DrawFilledRect(panelRect, new Color(0.05f, 0.05f, 0.05f, 0.86f));
        DrawFilledRect(new Rect(panelRect.x, panelRect.y, panelRect.width, 6f), stageColor);
        DrawFilledRect(badgeRect, stageColor);

        GUI.Label(headerRect, header, calibrationHeaderStyle);
        GUI.Label(bodyRect, body, calibrationBodyStyle);
        GUI.Label(badgeRect, badge, calibrationBadgeStyle);
    }

    private void EnsureCalibrationGuiStyles()
    {
        if (calibrationHeaderStyle == null)
        {
            calibrationHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
            };
        }

        if (calibrationBodyStyle == null)
        {
            calibrationBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.96f, 0.96f, 1f) },
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
        }

        if (calibrationBadgeStyle == null)
        {
            calibrationBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.black },
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
            };
        }
    }

    private static Color GetCalibrationStageColor(int stage)
    {
        switch (stage)
        {
            case 0:
                return new Color(0.94f, 0.35f, 0.13f, 0.95f); // closed pose      — orange
            case 1:
                return new Color(0.10f, 0.90f, 0.75f, 0.95f); // arms forward (Z) — teal
            case 2:
                return new Color(0.95f, 0.72f, 0.12f, 0.95f); // T-pose closed    — yellow
            case 3:
                return new Color(0.20f, 0.78f, 0.93f, 0.95f); // T-pose open      — cyan/blue
            case 4:
                return new Color(0.67f, 0.42f, 0.93f, 0.95f); // arms up          — purple
            default:
                return new Color(0.75f, 0.75f, 0.75f, 0.95f);
        }
    }

    private static void DrawFilledRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void EnsureCalibrationAudioSource()
    {
        if (calibrationAudioSource != null)
        {
            return;
        }

        calibrationAudioSource = GetComponent<AudioSource>();

        if (calibrationAudioSource == null)
        {
            calibrationAudioSource = gameObject.AddComponent<AudioSource>();
        }

        calibrationAudioSource.playOnAwake = false;
        calibrationAudioSource.spatialBlend = 0.0f;
    }

    private void PlayCountdownTone()
    {
        PlayTone(countdownToneHz, toneDurationSeconds, toneVolume);
    }

    private void PlayPhaseTone()
    {
        PlayTone(phaseToneHz, toneDurationSeconds * 1.2f, Mathf.Clamp01(toneVolume + 0.05f));
    }

    private void PlayTone(float frequencyHz, float durationSeconds, float volume)
    {
        if (!enableCalibrationSound || !Application.isPlaying)
        {
            return;
        }

        EnsureCalibrationAudioSource();

        int sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * durationSeconds));
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            data[i] = Mathf.Sin(2.0f * Mathf.PI * frequencyHz * t) * volume;
        }

        AudioClip clip = AudioClip.Create("calibration_tone", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        calibrationAudioSource.PlayOneShot(clip);
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
                jointRenderer.enabled = showSkeleton;
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
                boneRenderer.enabled = showSkeleton;
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

    public Transform GetJointTransform(string jointName)
    {
        Transform jointTransform;
        return jointTransforms.TryGetValue(jointName, out jointTransform) ? jointTransform : null;
    }

    /// <summary>
    /// True while a calibration sequence is actively running in Play mode.
    /// Read by <see cref="TabsUnitRigDriver"/> to display the reference pose.
    /// </summary>
    public bool IsCalibrating  => calibrationRunning && Application.isPlaying;

    /// <summary>
    /// Current calibration step index (0–4), or –1 when not running.
    /// </summary>
    public int  CalibrationStep => calibrationStage;

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

    private struct CalibrationSnapshot
    {
        public float TorsoHeight;
        public float WristDistance;
        public float AnkleDistance;
        public float ShoulderCenterY;
        public float WristCenterY;
        public float LeftWristY;
        public float RightWristY;
        public float LeftShoulderY;
        public float RightShoulderY;
        public float NoseY;
        // Depth fields used for Z-gain calibration (pose 2: arms forward).
        // MediaPipe z is in normalised units (same scale as landmark.x, NOT pixel-scaled).
        // More negative = closer to camera.
        public float BodyDepthCenter; // average z of shoulders + hips
        public float WristCenterZ;    // average z of left + right wrist
    }
}
