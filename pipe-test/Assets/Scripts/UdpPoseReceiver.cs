using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class PosePacket
{
    public long timestamp_ms;
    public FrameData frame;
    public PlayerPose[] players;
}

[Serializable]
public class FrameData
{
    public float width;
    public float height;
}

[Serializable]
public class PlayerPose
{
    public int id;
    public JointCollection joints;
    public BoneCollection bones;
}

[Serializable]
public class JointCollection
{
    public JointPoint nose;

    public JointPoint left_shoulder;
    public JointPoint right_shoulder;

    public JointPoint left_elbow;
    public JointPoint right_elbow;

    public JointPoint left_wrist;
    public JointPoint right_wrist;

    public JointPoint left_hip;
    public JointPoint right_hip;

    public JointPoint left_knee;
    public JointPoint right_knee;

    public JointPoint left_ankle;
    public JointPoint right_ankle;

    public JointPoint left_foot_index;
    public JointPoint right_foot_index;
}

[Serializable]
public class JointPoint
{
    public float x;
    public float y;
    public float z;
    public float visibility;
}

[Serializable]
public class BoneCollection
{
    public BoneVector left_upper_arm;
    public BoneVector left_lower_arm;
    public BoneVector right_upper_arm;
    public BoneVector right_lower_arm;

    public BoneVector left_upper_leg;
    public BoneVector left_lower_leg;
    public BoneVector right_upper_leg;
    public BoneVector right_lower_leg;

    public BoneVector head;
}

[Serializable]
public class BoneVector
{
    public float x;
    public float y;
    public float z;
    public float confidence;
}

public class UdpPoseReceiver : MonoBehaviour
{
    public int port = 5052;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running;

    private readonly object packetLock = new object();
    private PosePacket latestPacket;

    private void Start()
    {
        udpClient = new UdpClient(port);
        running = true;

        receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
        };
        receiveThread.Start();

        Debug.Log($"pipe-test listening for MediaPipe UDP packets on port {port}");
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, port);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(data);
                PosePacket packet = JsonUtility.FromJson<PosePacket>(json);

                lock (packetLock)
                {
                    latestPacket = packet;
                }
            }
            catch
            {
                // Ignore socket closure and malformed packet errors.
            }
        }
    }

    public PosePacket GetLatestPacket()
    {
        lock (packetLock)
        {
            return latestPacket;
        }
    }

    public PlayerPose GetPlayer(int playerId)
    {
        PosePacket packet = GetLatestPacket();

        if (packet == null || packet.players == null)
        {
            return null;
        }

        foreach (PlayerPose player in packet.players)
        {
            if (player != null && player.id == playerId)
            {
                return player;
            }
        }

        return null;
    }

    private void OnDestroy()
    {
        running = false;

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(200);
            receiveThread = null;
        }
    }
}
