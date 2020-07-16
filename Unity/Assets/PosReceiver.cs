using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;

public class PosReceiver : MonoBehaviour {
    float scale_ratio = 0.001f;  // pos.txtとUnityモデルのスケール比率
                                 // pos.txtの単位はmmでUnityはmのため、0.001に近い値を指定。モデルの大きさによって調整する
    float heal_position = 0.05f; // 足の沈みの補正値(単位：m)。プラス値で体全体が上へ移動する
    float head_angle = 15f; // 顔の向きの調整 顔を15度上げる

    public bool debug_cube; // デバッグ用Cubeの表示フラグ

    float play_time; // 再生時間 
    Transform[] bone_t; // モデルのボーンのTransform
    Transform[] cube_t; // デバック表示用のCubeのTransform
    Vector3 init_position; // 初期のセンターの位置
    Quaternion[] init_rot; // 初期の回転値
    Quaternion[] init_inv; // 初期のボーンの方向から計算されるクオータニオンのInverse
    List<Vector3[]> pos; // pos.txtのデータを保持するコンテナ
    int[] bones = new int[10] { 1, 2, 4, 5, 7, 8, 11, 12, 14, 15 }; // 親ボーン
    int[] child_bones = new int[10] { 2, 3, 5, 6, 8, 10, 12, 13, 15, 16 }; // bonesに対応する子ボーン
    int bone_num = 17;
    Animator anim;
    int s_frame;
    int e_frame;

    private WebSocket ws;
    bool isReceived = false;
    private string receivedJson;
    private BodyParts3 bodyParts;

    float[] x = new float[17];
    float[] y = new float[17];
    float[] z = new float[17];

    Vector3[] vs;

    // pos.txtのデータを読み込み、リストで返す
    List<Vector3[]> ReadPosData()
    {
        List<Vector3[]> data = new List<Vector3[]>();

        if (isReceived)
        {
            bodyParts = JsonUtility.FromJson<BodyParts3>(receivedJson);
            for (int i = 0; i < 17; i++)
            {
                x[i] = bodyParts.body_parts[i].x;
                y[i] = bodyParts.body_parts[i].y;
                z[i] = bodyParts.body_parts[i].z;
            }

            isReceived = false;
        }
        for (int i = 0; i < 17; i++)
        {
            vs[i] = new Vector3(-x[i], y[i], -z[i]);
        }
        data.Add(vs);

        return data;
    }

    // BoneTransformの取得。回転の初期値を取得
    void GetInitInfo()
    {
        bone_t = new Transform[bone_num];
        init_rot = new Quaternion[bone_num];
        init_inv = new Quaternion[bone_num];

        bone_t[0] = anim.GetBoneTransform(HumanBodyBones.Hips);
        bone_t[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        bone_t[2] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        bone_t[3] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        bone_t[4] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        bone_t[5] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        bone_t[6] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        bone_t[7] = anim.GetBoneTransform(HumanBodyBones.Spine);
        bone_t[8] = anim.GetBoneTransform(HumanBodyBones.Neck);
        bone_t[10] = anim.GetBoneTransform(HumanBodyBones.Head);
        bone_t[11] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        bone_t[12] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        bone_t[13] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        bone_t[14] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        bone_t[15] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        bone_t[16] = anim.GetBoneTransform(HumanBodyBones.RightHand);

        // Spine,LHip,RHipで三角形を作ってそれを前方向とする。
        Vector3 init_forward = TriangleNormal(bone_t[7].position, bone_t[4].position, bone_t[1].position);
        init_inv[0] = Quaternion.Inverse(Quaternion.LookRotation(init_forward));

        init_position = bone_t[0].position;
        init_rot[0] = bone_t[0].rotation;
        for (int i = 0; i < bones.Length; i++)
        {
            int b = bones[i];
            int cb = child_bones[i];

            // 対象モデルの回転の初期値
            init_rot[b] = bone_t[b].rotation;
            // 初期のボーンの方向から計算されるクオータニオン
            init_inv[b] = Quaternion.Inverse(Quaternion.LookRotation(bone_t[b].position - bone_t[cb].position, init_forward));
        }
    }

    // 指定の3点でできる三角形に直交する長さ1のベクトルを返す
    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;

        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();

        return dd;
    }

    // デバック用cubeを生成する。生成済みの場合は位置を更新する
    void UpdateCube(int frame)
    {
        if (cube_t == null)
        {
            // 初期化して、cubeを生成する
            cube_t = new Transform[bone_num];

            for (int i = 0; i < bone_num; i++)
            {
                Transform t = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                t.transform.parent = this.transform;
                t.localPosition = pos[frame][i] * scale_ratio;
                t.name = i.ToString();
                t.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                cube_t[i] = t;

                Destroy(t.GetComponent<BoxCollider>());
            }
        }
        else
        {
            // モデルと重ならないように少しずらして表示
            Vector3 offset = new Vector3(1.2f, 0, 0);

            // 初期化済みの場合は、cubeの位置を更新する
            for (int i = 0; i < bone_num; i++)
            {
                cube_t[i].localPosition = pos[frame][i] * scale_ratio + new Vector3(0, heal_position, 0) + offset;
            }
        }
    }

    void Start()
    {
        vs = new Vector3[bone_num];

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

        anim = GetComponent<Animator>();
        play_time = 0;

        pos = ReadPosData();

        GetInitInfo();
    }

    void Update()
    {
        ws.Send("");

        if (pos == null)
        {
            return;
        }
        play_time += Time.deltaTime;

        int frame = s_frame + (int)(play_time * 30.0f);  // pos.txtは30fpsを想定
        if (frame > e_frame)
        {
            play_time = 0;  // 繰り返す
            frame = s_frame;
        }

        if (debug_cube)
        {
            UpdateCube(frame); // デバッグ用Cubeを表示する
        }

        Vector3[] now_pos = pos[frame];

        // センターの移動と回転
        Vector3 pos_forward = TriangleNormal(now_pos[7], now_pos[4], now_pos[1]);
        bone_t[0].position = now_pos[0] * scale_ratio + new Vector3(init_position.x, heal_position, init_position.z);
        bone_t[0].rotation = Quaternion.LookRotation(pos_forward) * init_inv[0] * init_rot[0];

        // 各ボーンの回転
        for (int i = 0; i < bones.Length; i++)
        {
            int b = bones[i];
            int cb = child_bones[i];
            bone_t[b].rotation = Quaternion.LookRotation(now_pos[b] - now_pos[cb], pos_forward) * init_inv[b] * init_rot[b];
        }

        // 顔の向きを上げる調整。両肩を結ぶ線を軸として回転
        bone_t[8].rotation = Quaternion.AngleAxis(head_angle, bone_t[11].position - bone_t[14].position) * bone_t[8].rotation;
    }
}

[System.Serializable]
public class BodyParts3
{
    public Position3[] body_parts;
}

[System.Serializable]
public class Position3
{
    public int id;
    public float x;
    public float y;
    public float z;
}