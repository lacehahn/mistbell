using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>一张「机位」：相机世界位置 + 欧拉角 + FOV。运行时截图器逐个套用。</summary>
[Serializable]
public struct ShotPose
{
    public string name;
    public Vector3 position;
    public Vector3 euler;
    public float fov;
}

/// <summary>
/// 机位表：编辑期由 SpikeScene 写好，运行时由 Screenshotter 按 -shots 逐个套用，
/// 这样一次出包就能拍多张不同构图的证据图（D03 的 A/B 对比也复用这个机制）。
/// </summary>
public class ShotRig : MonoBehaviour
{
    public List<ShotPose> poses = new List<ShotPose>();

    /// <summary>以「看向 target、俯角 pitch、距离 dist、水平偏转 yaw」定义机位。</summary>
    public static ShotPose Make(string name, Vector3 target, float pitch, float dist, float yaw, float fov = 28f)
    {
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        return new ShotPose
        {
            name = name,
            position = target - rot * Vector3.forward * dist,
            euler = rot.eulerAngles,
            fov = fov,
        };
    }

    public static ShotPose Look(string name, Vector3 eye, Vector3 target, float fov)
    {
        return new ShotPose { name = name, position = eye,
            euler = Quaternion.LookRotation(target - eye).eulerAngles, fov = fov };
    }

    public void Apply(int index)
    {
        if (poses == null || index < 0 || index >= poses.Count) return;
        var cam = Camera.main;
        if (cam == null) return;
        var p = poses[index];
        cam.transform.position = p.position;
        cam.transform.rotation = Quaternion.Euler(p.euler);
        cam.fieldOfView = p.fov;
    }
}
