using System.Linq;
using UnityEngine;

// Retain the legacy flame spheres outdoors; use the new flame sheets indoors.
public sealed class InteriorMaterialsRuntime : MonoBehaviour
{
    public ChurchInterior interior;
    MeshRenderer[] oldFlames;
    bool? active;
    void Awake()
    {
        oldFlames=interior.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r=>r.name.StartsWith("Interior_Candle_") && r.name.EndsWith("_Flame")).ToArray();
        // These renderers change state after entry, so must retain individual visibility.
        foreach(var r in oldFlames){var block=new MaterialPropertyBlock();block.SetFloat("_MaterialsState",1);r.SetPropertyBlock(block);}
    }
    void LateUpdate()
    {
        bool inside=interior.detailRoot.activeSelf;if(active==inside)return;active=inside;
        foreach(var r in oldFlames)r.enabled=!inside;
    }
}
