using System;
using UnityEngine;

// Shared source of truth for the door, flat floor and indoor state.
public sealed class ChurchInterior : MonoBehaviour
{
    public Transform doorLeaf;
    public Collider doorCollider;
    public MeshCollider floor;
    public Collider threshold;
    public Light[] lamps;
    public Renderer[] shafts;
    public Light chandelierLamp;
    public GameObject detailRoot;
    public Renderer legacyChandelier;
    public Renderer[] pews;
    public Material[] pewOutdoorMaterials;
    public Material pewWood, pewLegs;
    public int floorSlabs;
    void SetDetail(bool value)
    {
        if(detailRoot!=null) detailRoot.SetActive(value);
        if(floor!=null) floor.GetComponent<Renderer>().enabled=!value;
        if(legacyChandelier!=null) legacyChandelier.enabled=!value;
        if(pews!=null) for(int i=0;i<pews.Length;i++) pews[i].sharedMaterials=value?new[]{pewWood,pewLegs}:new[]{pewOutdoorMaterials[i],pewOutdoorMaterials[i]};
    }
    public Vector3 indoorChandelierPosition, outdoorChandelierPosition;
    public Renderer shell;
    public Material indoorStone;
    Material[] outdoorStone;
    Quaternion closedRotation;
    float openingTime;
    bool opening, inside;
    Color sky, equator, ambientGround;
    float ambientIntensity, fogDensity;
    public float OpenDegrees { get; private set; }
    public float FloorY => floor.bounds.max.y;
    public Vector3 DoorCenter => new Vector3(-2, FloorY + 1.8f, -8.08f);
    public static readonly Vector3 CameraOffset = new Vector3(0, 1.75f, -5.2f);

    void Awake()
    {
        SetDetail(false);
        if(shell!=null) outdoorStone=shell.sharedMaterials;
        foreach(var lamp in lamps) if(lamp.name=="Interior_NaveFill" || lamp.name.StartsWith("Interior_ChandelierCandle_")) lamp.enabled=false;
        if(chandelierLamp!=null) chandelierLamp.transform.position=outdoorChandelierPosition;
        closedRotation = doorLeaf.localRotation;
        sky = RenderSettings.ambientSkyColor; equator = RenderSettings.ambientEquatorColor;
        ambientGround = RenderSettings.ambientGroundColor;
        ambientIntensity = RenderSettings.ambientIntensity; fogDensity = RenderSettings.fogDensity;
    }
    public bool DoorInRange(Vector3 p) => Vector2.Distance(new Vector2(p.x,p.z),new Vector2(-2,-8.08f)) <= 2.5f &&
        Mathf.Abs(p.y-FloorY)<2;
    public bool TryOpen(Vector3 p)
    {
        if (!DoorInRange(p) || opening || OpenDegrees >= 99.9f) return false;
        opening = true; openingTime = 0;
        return true;
    }
    Vector3[] shaftOrigins;
    void LateUpdate()
    {
        if(shafts==null) return;
        if(shaftOrigins==null) shaftOrigins=Array.ConvertAll(shafts,r=>r.transform.position);
        for(int i=0;i<shafts.Length;i++)
        {
            float phase=Time.time*.3f*2*Mathf.PI+i*2.39996f;
            shafts[i].transform.position=shaftOrigins[i]+new Vector3(Mathf.Sin(phase)*.035f,0,Mathf.Cos(phase+1.7f)*.025f);
        }
    }
    void Update()
    {
        if (!opening) return;
        openingTime += Time.deltaTime;
        OpenDegrees = 100 * Mathf.SmoothStep(0,1,Mathf.Clamp01(openingTime/.8f));
        doorLeaf.localRotation = closedRotation * Quaternion.AngleAxis(-OpenDegrees,Vector3.up);
        // The closed leaf blocks the passage until its entire sweep has cleared it.
        if (OpenDegrees >= 95) doorCollider.enabled = false;
        Physics.SyncTransforms();
        if (openingTime >= .8f) opening = false;
    }
    public bool SupportAt(Vector3 p, out RaycastHit hit)
    {
        var ray=new Ray(new Vector3(p.x,500,p.z),Vector3.down);
        bool found=floor.Raycast(ray,out hit,1000);
        if(threshold.Raycast(ray,out var rampHit,1000) && (!found || rampHit.point.y>hit.point.y)) { hit=rampHit; found=true; }
        return found;
    }
    public static bool InPlan(Vector3 p, float margin=0)
    {
        bool nave = p.x > -5.9f-margin && p.x < 1.9f+margin && p.z > -7.4f-margin && p.z < 7.4f+margin;
        bool transept = p.x > -8.4f-margin && p.x < 4.4f+margin && Mathf.Abs(p.z)<2.9f+margin;
        bool apse = Vector2.Distance(new Vector2(p.x,p.z),new Vector2(-2,8.6f)) < 2.6f+margin;
        return nave || transept || apse;
    }
    public bool Contains(Vector3 p) => (InPlan(p) || (p.x>=-4.5f && p.x<=4.5f && p.z>=-9 && p.z<=6)) && Mathf.Abs(p.y-FloorY)<=.3f;
    public void SetInside(bool value)
    {
        if (value == inside || PlayerController.CaptureMode) return;
        inside = value;
        SetDetail(value);
        foreach(var lamp in lamps) if(lamp.name=="Interior_NaveFill" || lamp.name.StartsWith("Interior_ChandelierCandle_")) lamp.enabled=value;
        if(chandelierLamp!=null) chandelierLamp.transform.position=value?indoorChandelierPosition:outdoorChandelierPosition;
        RenderSettings.ambientSkyColor = value ? new Color32(0x5A,0x4A,0x3C,255) : sky;
        RenderSettings.ambientEquatorColor = value ? new Color32(0x43,0x38,0x2E,255) : equator;
        RenderSettings.ambientGroundColor = value ? new Color32(0x2A,0x23,0x20,255) : ambientGround;
        if(shell!=null) shell.sharedMaterials=value ? new[]{indoorStone} : outdoorStone;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.fogDensity = value ? fogDensity*.12f : fogDensity;
    }
}
