using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

// Geometry masks classify the first visible opaque surface, never the hidden target bounds.
// Screenshot thresholds are display Rec.709; rib percentages describe base material colors.
public static class InteriorToneAudit
{
    const int Width=480, Height=270, QueryLayer=29;
    static readonly Color32[] Colors={new Color32(0,0,0,255),new Color32(255,0,0,255),new Color32(0,0,255,255),new Color32(0,255,0,255),new Color32(255,255,0,255),new Color32(255,0,255,255),new Color32(0,255,255,255)};
    static float Luma(Color c)=>.2126f*c.r+.7152f*c.g+.0722f*c.b;
    static double Linear(double c)=>c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);
    static double insideNave=double.NaN;
    static int insideMullions;
    static int insideFrame=-1;
    static int Classify(string n)
    {
        if(n=="Main_Shell")return 1;
        if(n.StartsWith("Interior_Ceiling_"))return 2;
        return 0;
    }
    static string ShotPath(string filename)
    {
        var dir=new DirectoryInfo(Application.dataPath);
        while(dir!=null && dir.Name!="Builds") dir=dir.Parent;
        string root=dir!=null?dir.Parent.FullName:Directory.GetCurrentDirectory();
        string shots=Path.Combine(root,"Shots");Directory.CreateDirectory(shots);return Path.Combine(shots,filename);
    }
    sealed class Samples
    {
        public readonly double[] sums=new double[7], lstars=new double[7]; public readonly int[] counts=new int[7];
        public double Avg(int id)=>counts[id]>0?sums[id]/counts[id]:0;
        public double LStar(int id)=>counts[id]>0?lstars[id]/counts[id]:0;
    }
    static bool HasNondegenerateTriangle(Mesh mesh,int onlySubmesh=-1)
    {
        // Unreadable meshes may still be assigned directly to MeshCollider. Never read their buffers.
        if(!mesh.isReadable)return true;
        var vertices=mesh.vertices;
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            if(onlySubmesh>=0 && sub!=onlySubmesh)continue;
            if(mesh.GetTopology(sub)!=MeshTopology.Triangles)continue;
            var indices=mesh.GetTriangles(sub);
            for(int i=0;i+2<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                float areaSquared=Vector3.Cross(b-a,c-a).sqrMagnitude;
                if(!float.IsInfinity(areaSquared) && areaSquared>1e-16f)return true;
            }
        }
        return false;
    }
    static Samples Measure(Camera camera,Texture2D frame,string filename)
    {
        var queries=new List<GameObject>();var labels=new Dictionary<Collider,int>();var names=new Dictionary<Collider,string>();
        var mask=new Texture2D(Width,Height,TextureFormat.RGB24,false);
        var pixels=new Color32[Width*Height];var samples=new Samples();
        // Measure the delivered 8-bit PNG, avoiding GPU capture/PNG quantization differences.
        var savedFrame=new Texture2D(2,2,TextureFormat.RGBA32,false);
        string view=filename.Contains("inside")?"inside":"lookup";
        if(!savedFrame.LoadImage(File.ReadAllBytes(ShotPath("interior_"+view+".png"))))throw new InvalidDataException("Could not decode delivered screenshot");
        frame=savedFrame;
        bool oldBackfaces=Physics.queriesHitBackfaces;
        var frustum=GeometryUtility.CalculateFrustumPlanes(camera);
        int skippedEmpty=0,skippedDegenerate=0,skippedFrustum=0;
        try
        {
            foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                // Runtime batching preserves enabled original meshes for diagnostic queries.
                // Query those labeled originals, not the merged, unlabeled duplicate geometry.
                if(renderer.name.StartsWith("StaticArtBatch_") || renderer.name=="StaticPewBatch") continue;
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                   (camera.cullingMask & (1<<renderer.gameObject.layer))==0) continue;
                if(!GeometryUtility.TestPlanesAABB(frustum,renderer.bounds)){skippedFrustum++;continue;}
                var filter=renderer.GetComponent<MeshFilter>();var materials=renderer.sharedMaterials;
                if(filter==null || filter.sharedMesh==null || materials.Length==0 || materials.All(m=>m==null || m.renderQueue>=2500)) continue;
                // Split material slots so a transparent submesh never becomes an opaque blocker.
                var source=filter.sharedMesh;
                ulong indexCount=0;for(int sub=0;sub<source.subMeshCount;sub++)indexCount+=source.GetIndexCount(sub);
                if(source.vertexCount<3 || indexCount<3){skippedEmpty++;continue;}
                if(!HasNondegenerateTriangle(source)){skippedDegenerate++;continue;}
                bool allOpaque=materials.All(m=>m!=null && m.renderQueue<2500);
                if(!allOpaque && !source.isReadable) throw new InvalidOperationException("Windows audit cannot split unreadable mixed-opacity mesh: "+renderer.name);
                for(int sub=0;sub<(allOpaque?1:source.subMeshCount);sub++)
                {
                    var material=materials[Math.Min(sub,materials.Length-1)];
                    if(material==null || material.renderQueue>=2500) continue;
                    if(!allOpaque && (source.GetIndexCount(sub)<3 || source.GetTopology(sub)!=MeshTopology.Triangles)){skippedEmpty++;continue;}
                    if(!allOpaque && !HasNondegenerateTriangle(source,sub)){skippedDegenerate++;continue;}
                    var go=new GameObject("WindowsAuditQuery");queries.Add(go);go.layer=QueryLayer;
                    go.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);go.transform.localScale=renderer.transform.lossyScale;
                    var collider=go.AddComponent<MeshCollider>();
                    if(allOpaque || source.subMeshCount==1) collider.sharedMesh=source;
                    else
                    {
                        var mesh=new Mesh {name="WindowsAuditSubmesh",indexFormat=source.indexFormat};
                        mesh.vertices=source.vertices;mesh.triangles=source.GetTriangles(sub);mesh.RecalculateBounds();collider.sharedMesh=mesh;
                    }
                    labels[collider]=Classify(renderer.name);names[collider]=renderer.name;
                }
            }
            Debug.Log($"[WINDOWS_QUERY] view={(filename.Contains("inside")?"inside":"lookup")} colliders={queries.Count} skipped_empty={skippedEmpty} skipped_degenerate={skippedDegenerate} skipped_frustum={skippedFrustum}");
            Physics.SyncTransforms();Physics.queriesHitBackfaces=true;
            if(false)
            {
                foreach(var viewport in new[]{new Vector3(.265f,.90f,0),new Vector3(.735f,.90f,0),new Vector3(.735f,.52f,0)})
                {
                    var hits=Physics.RaycastAll(camera.ViewportPointToRay(viewport),camera.farClipPlane,1<<QueryLayer,QueryTriggerInteraction.Ignore)
                        .Where(h=>names.ContainsKey(h.collider)).OrderBy(h=>h.distance).Take(3).ToArray();
                    string Point(Vector3 p)=>FormattableString.Invariant($"({p.x:F4},{p.y:F4},{p.z:F4})");
                    string top=string.Join(";",hits.Select(h=>FormattableString.Invariant($"{names[h.collider]}@{h.distance:F4}:{Point(h.point)}")));
                    string nearest=hits.Length>0?names[hits[0].collider]:"none";
                    string world=hits.Length>0?Point(hits[0].point):"none";
                    Debug.Log(FormattableString.Invariant($"[WINDOWS_HIT] view=inside x={viewport.x:F3} y={viewport.y:F3} nearest={nearest} world={world} top3={top} coordinates=bottom_left opaque_query_only=true"));
                }
            }
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
            {
                var viewport=new Vector3((x+.5f)/Width,(y+.5f)/Height,0);
                var hits=Physics.RaycastAll(camera.ViewportPointToRay(viewport),camera.farClipPlane,1<<QueryLayer,QueryTriggerInteraction.Ignore);
                int id=0;float distance=float.PositiveInfinity;
                foreach(var hit in hits) if(hit.distance<distance && labels.TryGetValue(hit.collider,out int label)) {distance=hit.distance;id=label;}
                if(id==1) {
                    var near=hits.OrderBy(h=>h.distance).First();
                    if(near.point.z>7)id=3;
                    else if(near.point.x < -4)id=4;
                    else if(near.point.x > 0)id=5;
                    else id=0;
                }
                pixels[y*Width+x]=Colors[id];if(id==0)continue;
                var rgb=frame.GetPixel((2*x+1)*frame.width/(2*Width),(2*y+1)*frame.height/(2*Height));
                samples.sums[id]+=Luma(rgb);
                double linearY=.2126*Linear(rgb.r)+.7152*Linear(rgb.g)+.0722*Linear(rgb.b);
                samples.lstars[id]+=linearY>216.0/24389.0?116*Math.Pow(linearY,1.0/3.0)-16:(24389.0/27.0)*linearY;
                samples.counts[id]++;
            }
            mask.SetPixels32(pixels);mask.Apply();File.WriteAllBytes(ShotPath(filename),mask.EncodeToPNG());
        }
        finally
        {
            Physics.queriesHitBackfaces=oldBackfaces;
            foreach(var go in queries)
            {
                var mesh=go.GetComponent<MeshCollider>().sharedMesh;
                go.SetActive(false);Object.Destroy(go);
                if(mesh!=null && mesh.name=="WindowsAuditSubmesh")Object.Destroy(mesh);
            }
            Object.Destroy(mask);Object.Destroy(savedFrame);
        }
        return samples;
    }
    public static void Dome(ChurchInterior interior,Camera cam,Texture2D frame) { Measure(cam,frame,"interior_tone_lookup_mask.png"); }
    public static void Inside(ChurchInterior interior,Camera cam,Texture2D frame)
    {
        Measure(cam,frame,"interior_tone_inside_mask.png");
    }
}
