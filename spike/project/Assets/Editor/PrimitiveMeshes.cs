using System.Collections.Generic;
using UnityEngine;

/// <summary>体量网格；每面独立顶点，UV 按面内距离（米）展开。</summary>
public static class PrimitiveMeshes
{
    static void Face(List<Vector3> v, List<Vector2> uv, List<int> t, params Vector3[] p)
    {
        int start = v.Count;
        var u = (p[1] - p[0]).normalized;
        var normal = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
        var w = Vector3.Cross(normal, u);
        foreach (var q in p) { v.Add(q); var d = q - p[0]; uv.Add(new Vector2(Vector3.Dot(d,u), Vector3.Dot(d,w))); }
        for (int i = 1; i < p.Length - 1; i++) { t.Add(start); t.Add(start+i); t.Add(start+i+1); }
    }

    public static GameObject Polygon(Transform parent, string name, int sides, float radius,
                                     float height, Vector3 pos, Material mat, bool cone = false)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a = i * 2 * Mathf.PI / sides, b = (i+1) * 2 * Mathf.PI / sides;
            var p = new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
            var q = new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
            if (cone) Face(v,uv,t,p,Vector3.up*height,q);
            else { Face(v,uv,t,p,p+Vector3.up*height,q+Vector3.up*height,q); Face(v,uv,t,Vector3.up*height,q+Vector3.up*height,p+Vector3.up*height); }
            Face(v,uv,t,Vector3.zero,p,q);
        }
        return Create(parent,name,pos,mat,v,uv,t);
    }

    public static GameObject Gable(Transform parent, string name, float width, float depth,
                                   float rise, Vector3 pos, Material mat, float yaw = 0)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        var a = new Vector3(-width/2,0,-depth/2); var b = new Vector3(width/2,0,-depth/2);
        var c = new Vector3(0,rise,-depth/2); var d = a+Vector3.forward*depth;
        var e = b+Vector3.forward*depth; var f = c+Vector3.forward*depth;
        Face(v,uv,t,a,c,b); Face(v,uv,t,d,e,f); Face(v,uv,t,a,d,f,c);
        Face(v,uv,t,b,c,f,e); Face(v,uv,t,a,b,e,d);
        var go = Create(parent,name,pos,mat,v,uv,t);
        go.transform.localRotation = Quaternion.Euler(0,yaw,0);
        return go;
    }

    static GameObject Create(Transform parent, string name, Vector3 pos, Material mat,
                             List<Vector3> v, List<Vector2> uv, List<int> t)
    {
        var mesh = new Mesh { name=name, vertices=v.ToArray(), uv=uv.ToArray(), triangles=t.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        var go = new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=pos;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        go.AddComponent<MeshCollider>().sharedMesh=mesh;
        return go;
    }
}
