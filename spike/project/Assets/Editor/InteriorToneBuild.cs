using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class InteriorToneBuild
{
    static bool Mutable(Renderer r)=>r.name.StartsWith("Interior_Ceiling") || r.name=="Interior_WallPainting" || r.name=="Interior_DomeInner" || r.name.StartsWith("Interior_ConeRib_") || r.name.StartsWith("Interior_DrumWall_");
    static string Protected(ChurchInterior i)
    {
        var lines=new List<string>();
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(Mutable(r))continue;
            lines.Add(r.name+EditorJsonUtility.ToJson(r)+r.transform.localToWorldMatrix.ToString("F6"));
            var mf=r.GetComponent<MeshFilter>();if(mf)lines.Add(EditorJsonUtility.ToJson(mf.sharedMesh));
            foreach(var m in r.sharedMaterials)if(m!=i.indoorStone && m!=i.wallFootStone)lines.Add(EditorJsonUtility.ToJson(m));
        }
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add(c.name+EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"));
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))lines.Add(l.name+EditorJsonUtility.ToJson(l)+l.transform.localToWorldMatrix.ToString("F6"));
        lines.Sort(StringComparer.Ordinal);return string.Join("\n",lines);
    }
    static void Tone(Material m,Color color,Color emission)
    {
        m.SetColor("_BaseColor",color);m.SetColor("_EmissionColor",emission);m.EnableKeyword("_EMISSION");EditorUtility.SetDirty(m);
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");var i=Object.FindFirstObjectByType<ChurchInterior>();
        string before=Protected(i);
        foreach(var m in new[]{i.indoorStone,i.wallFootStone})Tone(m,new Color(.50f,.48f,.45f),new Color(.239f,.210f,.191f));
        var rs=i.GetComponentsInChildren<MeshRenderer>(true);
        foreach(var r in rs.Where(Mutable))
        {
            if(r.name.StartsWith("Interior_CeilingBeam_") || r.name=="Interior_WallPainting"){r.enabled=false;continue;}
            if(r.name.StartsWith("Interior_Ceiling_"))
                Tone(r.sharedMaterial,new Color(.25f,.245f,.23f),new Color(.315f,.30f,.273f));
            if(r.name.StartsWith("Interior_DrumWall_"))Tone(r.sharedMaterial,new Color(.50f,.48f,.45f),new Color(.239f,.210f,.191f));
        }
        // Restore the pre-task dome plaster/ribs; ceiling grading excludes the dome aperture.
        foreach(var r in rs.Where(r=>r.name=="Interior_DomeInner" || r.name.StartsWith("Interior_ConeRib_"))) {
            var c=(Color)new Color32(156,139,116,255);if(r.name.StartsWith("Interior_ConeRib_"))c*=r.name.EndsWith("_0")?1.11f:.91f;
            Tone(r.sharedMaterial,c,c*.35f);
        }
        AssetDatabase.SaveAssets();string after=Protected(i);
        string Digest(string t){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(t))).Replace("-","").ToLowerInvariant();}
        System.IO.Directory.CreateDirectory("../artifacts/interior_tone");
        System.IO.File.WriteAllText("../artifacts/interior_tone/protected_digests.json","{\"before\":\""+Digest(before)+"\",\"after\":\""+Digest(after)+"\"}\n");
        if(before!=after)throw new Exception("Protected floor/exterior/camera/altar/icons/pews/royal door/colliders changed");
        Debug.Log("[TONE_PROTECTED] floor_exterior_camera_altar_icons_pews_royal_door_colliders=true");
        var stem=rs.First(r=>r.name=="Interior_Chain_CeilingStem");
        Debug.Log(FormattableString.Invariant($"[CHANDELIER_STEM] beam_width=0 removed=true stem_width={Mathf.Max(stem.bounds.size.x,stem.bounds.size.z):F3} beams_disabled={rs.Count(r=>r.name.StartsWith("Interior_CeilingBeam_")&&!r.enabled)} links={rs.Count(r=>r.name.StartsWith("Interior_Chain_Link"))}"));
        Debug.Log("[WALL_PAINTING] removed=true");
        EditorSceneManager.MarkSceneDirty(i.gameObject.scene);EditorSceneManager.SaveScene(i.gameObject.scene);PerformanceBuild.Run();
    }
}
