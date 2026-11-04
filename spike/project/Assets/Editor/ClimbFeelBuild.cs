using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Invoked after the last path/fence patch, before the existing performance build.
public static class ClimbFeelBuild
{
    public static void Apply() {
        var surface=GameObject.Find("Paving_Surface");
        var ground=Object.FindFirstObjectByType<PlayerController>().ground;
        string Protected() {
            var rows=new List<string>();
            foreach(var c in Object.FindObjectsByType<Component>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
                if(c==null||c.gameObject==surface)continue;
                rows.Add(c.GetInstanceID()+EditorJsonUtility.ToJson(c));
                if(c is Renderer r)foreach(var m in r.sharedMaterials)if(m!=null)rows.Add(EditorJsonUtility.ToJson(m));
                if(c is MeshFilter mf&&mf.sharedMesh!=null)rows.Add(EditorJsonUtility.ToJson(mf.sharedMesh));
            }
            rows.Sort(StringComparer.Ordinal);
            using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",rows))));
        }
        string protectedBefore=Protected();
        Vector3 At(float s,float x) {
            var p=ClimbFeel.Point(s);var d=(ClimbFeel.Point(s+.05f)-ClimbFeel.Point(s-.05f)).normalized;
            p+=new Vector2(d.y,-d.x)*x;
            if(!ground.Raycast(new Ray(new Vector3(p.x,400,p.y),Vector3.down),out var h,1000))throw new Exception("Climb support missing");
            return h.point+Vector3.up*.035f;
        }
        float[] bases=new float[3];
        var stations=new SortedSet<float>{0,ClimbFeel.Length};
        for(float s=0;s<ClimbFeel.Length;s+=.25f)stations.Add(s);
        for(int f=0;f<3;f++) {
            float a=ClimbFeel.FlightStart(f),b=a+ClimbFeel.Steps*ClimbFeel.Tread;
            // The platforms and solid treads sit above the unchanged snow terrain.
            float h=float.NegativeInfinity;
            for(float s=a-2;s<=b+2.001f;s+=.04f) {
                float lift=s<a?0:s>=b?1.6f:(Mathf.Floor((s-a)/.32f)+1)*.16f;
                foreach(float x in new[]{-1.05f,0,1.05f})h=Mathf.Max(h,At(s,x).y-lift+.02f);
            }
            bases[f]=h;
            stations.RemoveWhere(s=>s>a-2&&s<b+2);
            stations.Add(a-6);stations.Add(a-2);stations.Add(b+2);stations.Add(b+6);
            for(int k=0;k<=10;k++)stations.Add(a+k*.32f);
        }
        float Height(float s,float x,float side) {
            if(Mathf.Abs(x)>1.049f)return At(s,x).y; // 15cm shoulders meet the unchanged snow.
            for(int f=0;f<3;f++) {
                float a=ClimbFeel.FlightStart(f),b=a+3.2f;
                if(s<a-6||s>b+6)continue;
                if(s<a-2)return Mathf.Lerp(At(s,x).y,bases[f],Mathf.SmoothStep(0,1,(s-(a-6))/4));
                if(s>b+2)return Mathf.Lerp(bases[f]+1.6f,At(s,x).y,Mathf.SmoothStep(0,1,(s-(b+2))/4));
                if(s<a-.0001f)return bases[f];
                if(s>b-.0001f)return bases[f]+1.6f;
                int step=Mathf.Clamp(Mathf.FloorToInt((s-a+side*.0001f)/.32f)+1,0,10);
                return bases[f]+step*.16f;
            }
            return At(s,x).y;
        }
        var verts=new List<Vector3>();var uv=new List<Vector2>();var top=new List<int>();var dark=new List<int>();var road=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,List<int> tris) {
            int n=verts.Count;verts.AddRange(new[]{a,b,c,d});
            foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2((p.x+p.z)/2.4f,(p.y+p.z)/2.4f));
            tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        Vector3 V(float s,float x,float side){var p=At(s,x);p.y=Height(s,x,side);return p;}
        bool Stone(float s){for(int f=0;f<3;f++){float a=ClimbFeel.FlightStart(f);if(s>=a-2&&s<=a+5.2f)return true;}return false;}
        var samples=new List<float>(stations);
        for(int i=0;i<samples.Count-1;i++) {
            float a=samples[i],b=samples[i+1];bool stone=Stone((a+b)*.5f);
            float[] widths={-1.05f,-.9f,.9f,1.05f};
            for(int w=0;w<3;w++) {
                float l=widths[w],r=widths[w+1];
                Quad(V(a,l,1),V(b,l,-1),V(b,r,-1),V(a,r,1),stone&&w==1?top:road);
                // Vertical front closes every riser; split vertices preserve sharp face normals.
                if(Mathf.Abs(Height(a,0,1)-Height(a,0,-1))>.01f)
                    Quad(V(a,l,-1),V(a,l,1),V(a,r,1),V(a,r,-1),dark);
            }
            if(stone) {
                var al=V(a,-1.05f,1);var bl=V(b,-1.05f,-1);var ar=V(a,1.05f,1);var br=V(b,1.05f,-1);
                var abl=At(a,-1.05f)-Vector3.up*.2f;var bbl=At(b,-1.05f)-Vector3.up*.2f;
                var abr=At(a,1.05f)-Vector3.up*.2f;var bbr=At(b,1.05f)-Vector3.up*.2f;
                Quad(abl,bbl,bl,al,dark);Quad(ar,br,bbr,abr,dark);Quad(abr,bbr,bbl,abl,dark);
            }
        }
        const string asset="Assets/Settings/ClimbFeel_Surface.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
        if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,asset);}
        mesh.Clear();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.subMeshCount=3;
        mesh.SetTriangles(road,0);mesh.SetTriangles(top,1);mesh.SetTriangles(dark,2);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        Material StoneMat(string name,float tint) {
            string p="Assets/Settings/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
            if(m==null){m=new Material(surface.GetComponent<Renderer>().sharedMaterial);AssetDatabase.CreateAsset(m,p);}
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Path/path_stone_a.jpg"));
            m.SetFloat("_Steps",1);m.SetColor("_BaseColor",new Color(tint,tint,tint,1));EditorUtility.SetDirty(m);return m;
        }
        var original=surface.GetComponent<Renderer>().sharedMaterial;
        surface.GetComponent<MeshFilter>().sharedMesh=mesh;surface.GetComponent<MeshCollider>().sharedMesh=mesh;
        surface.GetComponent<Renderer>().sharedMaterials=new[]{original,StoneMat("M_ClimbTread",1),StoneMat("M_ClimbRiser",.9f)};
        Debug.Log(FormattableString.Invariant($"[CLIMB_STEPS] flights=3 steps=10 rise=0.16 tread=0.32 platforms=2.00 shoulder=0.15 stations={ClimbFeel.FlightStart(0):F3},{ClimbFeel.FlightStart(1):F3},{ClimbFeel.FlightStart(2):F3} triangles={mesh.triangles.Length/3}"));
        Physics.SyncTransforms();
        float riseMin=999,riseMax=0,flatError=0;
        float Probe(float station) {
            var p=ClimbFeel.Point(station);
            if(!surface.GetComponent<MeshCollider>().Raycast(new Ray(new Vector3(p.x,400,p.y),Vector3.down),out var h,1000))throw new Exception("Built climb collider missing");
            return h.point.y;
        }
        for(int f=0;f<3;f++) {
            float a=ClimbFeel.FlightStart(f),previous=Probe(a-.1f);
            for(int k=0;k<10;k++) {
                float y=Probe(a+(k+.5f)*.32f),rise=y-previous;
                riseMin=Mathf.Min(riseMin,rise);riseMax=Mathf.Max(riseMax,rise);previous=y;
                flatError=Mathf.Max(flatError,Mathf.Abs(Probe(a+k*.32f+.02f)-Probe(a+(k+1)*.32f-.02f)));
            }
            flatError=Mathf.Max(flatError,Mathf.Abs(Probe(a-1.99f)-Probe(a-.01f)));
            flatError=Mathf.Max(flatError,Mathf.Abs(Probe(a+3.21f)-Probe(a+5.19f)));
        }
        bool protectedSame=protectedBefore==Protected();
        Debug.Log(FormattableString.Invariant($"[CLIMB_GEOMETRY] rise_min={riseMin:F6} rise_max={riseMax:F6} flat_error={flatError:F6} protected_unchanged={protectedSame}"));
        if(riseMin<.14f||riseMax>.18f||flatError>.001f||!protectedSame)throw new Exception("Climb geometry/protection gate failed");
        AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(surface.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(surface.scene);
    }
}
