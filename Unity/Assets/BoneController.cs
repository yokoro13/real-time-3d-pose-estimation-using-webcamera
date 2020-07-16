using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using WebSocketSharp;
using WebSocketSharp.Net;

public class BoneController : MonoBehaviour
{
    private BodyParts bodyParts;
    private string receivedJson;
    private WebSocket ws;

    [SerializeField, Range(10, 120)] private float frameRate;
    public List<Transform> boneList = new List<Transform>();
    private GameObject fullbodyIK;
    private Vector3[] points = new Vector3[17];
    private Vector3[] normalizeBone = new Vector3[12];
    private float[] boneDistance = new float[12];
    private float timer;

    private int[,] joints = new int[,] {
        {0, 1}, {1, 2}, {2, 3}, {0, 4}, {4, 5}, {5, 6}, {0, 7}, {7, 8}, {8, 9}, {9, 10}, {8, 11}, {11, 12}, {12, 13}, {8, 14}, {14, 15}, {15, 16}
    };

    private int[,] boneJoint = new int[,] {{0, 2}, {2, 3}, {0, 5}, {5, 6}, {0, 9}, {9, 10}, {9, 11}, {11, 12}, {12, 13}, {9, 14}, {14, 15}, {15, 16}};

    private int[,] normalizeJoint = new int[,] {{0, 1}, {1, 2}, {0, 3}, {3, 4}, {0, 5}, {5, 6}, {5, 7}, {7, 8}, {8, 9}, {5, 10}, {10, 11}, {11, 12}};

    private int nowFrame = 0;

    private float[] x = new float[17];
    private float[] y = new float[17];
    private float[] z = new float[17];

    private bool isReceived = false;

    // Use this for initialization
    void Start()
    {
        ws = new WebSocket("ws://localhost:5000/");
        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("WebSocket Open");
        };
        ws.OnMessage += (sender, e) =>
        {
            receivedJson = e.Data;
            Debug.Log("Data: " + e.Data);
            isReceived = true;
        };
        ws.OnError += (sender, e) =>
        {
            Debug.Log("WebSocket Error Message: " + e.Message);
        };
        ws.OnClose += (sender, e) =>
        {
            Debug.Log("WebSocket Close");
        };
        ws.Connect();

        ws.Send("");
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        ws.Send("");

        if (timer > (1 / frameRate))
        {
            timer = 0;
            PointUpdate();
        }

        if (!fullbodyIK)
        {
            IKFind();
        }
        else
        {
            IKSet();
        }
    }

    private void OnDestroy()
    {
        ws.Close();
        ws = null;
    }

    private void PointUpdate()
    {
        if (nowFrame < 100)
        {
            nowFrame++;
            if (isReceived)
            {
                bodyParts = JsonUtility.FromJson<BodyParts>(receivedJson);
                for (int i = 0; i < 17; i++)
                {
                    x[i] = bodyParts.parts[i].x;
                    y[i] = bodyParts.parts[i].y;
                    z[i] = bodyParts.parts[i].z;
                }
                isReceived = false;
            }

            for (int i = 0; i < 17; i++)
            {
                points[i] = new Vector3(x[i], y[i], -z[i]);
            }

            for (int i = 0; i < 12; i++)
            {
                normalizeBone[i] = (points[boneJoint[i, 1]] - points[boneJoint[i, 0]]).normalized;
            }
        }
    }

    private void IKFind()
    {
        fullbodyIK = GameObject.Find("FullBodyIK");
        if (fullbodyIK)
        {
            for (int i = 0; i < Enum.GetNames(typeof(OpenPoseRef)).Length; i++)
            {
                Transform obj = GameObject.Find(Enum.GetName(typeof(OpenPoseRef), i)).transform;
                if (obj)
                {
                    boneList.Add(obj);
                }
            }

            for (int i = 0; i < Enum.GetNames(typeof(NormalizeBoneRef)).Length; i++)
            {
                boneDistance[i] = Vector3.Distance(boneList[normalizeJoint[i, 0]].position,
                    boneList[normalizeJoint[i, 1]].position);
            }
        }
    }

    private void IKSet()
    {
        if (Math.Abs(points[0].x) < 1000 && Math.Abs(points[0].y) < 1000 && Math.Abs(points[0].z) < 1000)
        {
            boneList[0].position = points[0] * 0.001f + Vector3.up * 0.8f;
        }

        for (int i = 0; i < 12; i++)
        {
            boneList[normalizeJoint[i, 1]].position = Vector3.Lerp(
                boneList[normalizeJoint[i, 1]].position,
                boneList[normalizeJoint[i, 0]].position + boneDistance[i] * normalizeBone[i]
                , 0.05f
            );
            DrawLine(boneList[normalizeJoint[i, 0]].position, boneList[normalizeJoint[i, 1]].position, Color.red);
        }

        for (int i = 0; i < joints.Length / 2; i++)
        {
            DrawLine(points[joints[i, 0]] * 0.001f + new Vector3(-1, 0.8f, 0),
                points[joints[i, 1]] * 0.001f + new Vector3(-1, 0.8f, 0), Color.blue);
        }
    }

    private void DrawLine(Vector3 s, Vector3 e, Color c)
    {
        Debug.DrawLine(s, e, c);
    }
}

internal enum OpenPoseRef
{
    Hips,
    LeftKnee,
    LeftFoot,
    RightKnee,
    RightFoot,
    Neck,
    Head,
    RightArm,
    RightElbow,
    RightWrist,
    LeftArm,
    LeftElbow,
    LeftWrist
};

internal enum NormalizeBoneRef
{
    Hip2LeftKnee,
    LeftKnee2LeftFoot,
    Hip2RightKnee,
    RightKnee2RightFoot,
    Hip2Neck,
    Neck2Head,
    Neck2RightArm,
    RightArm2RightElbow,
    RightElbow2RightWrist,
    Neck2LeftArm,
    LeftArm2LeftElbow,
    LeftElbow2LeftWrist
};

[System.Serializable]
public class BodyParts
{
    public Position[] parts;
}

[System.Serializable]
public class Position
{
    public float x;
    public float y;
    public float z;
}