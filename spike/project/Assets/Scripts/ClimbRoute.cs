using UnityEngine;
using System.Collections.Generic;

// Metres, shared by scene generation and runtime traversal checks.
public static class ClimbRoute
{
    public const float Width = 5f, Depth = .25f;
    public static Vector3 Spawn => new Vector3(CenterX(-205), 0, -205);
    public static readonly Vector2[] Points = {
        new Vector2(-18,-210), new Vector2(10,-150), new Vector2(-12,-95),
        new Vector2(0,-40), new Vector2(0,-13.25f) };
    static readonly float[] Grades = {6,8,10};
    public const float PavingWidth = 2.1f, SnowEdge = .07f;
    // Quadratic Bezier fillets, tangent to the unchanged straight approaches.
    // Height(z) and the original grade control points intentionally remain unchanged.
    public static readonly Vector2[] SmoothPoints = SamplePlan();
    static Vector2[] SamplePlan()
    {
        var dense=new List<Vector2>{Points[0]};
        void Line(Vector2 p) { var a=dense[dense.Count-1];int n=Mathf.CeilToInt(Vector2.Distance(a,p)/.05f);for(int j=1;j<=n;j++)dense.Add(Vector2.Lerp(a,p,j/(float)n)); }
        for(int i=1;i<Points.Length-1;i++) {
            var c=Points[i];var a=c+(Points[i-1]-c).normalized*10f;var b=c+(Points[i+1]-c).normalized*10f;
            Line(a);for(int j=1;j<=400;j++){float t=j/400f;dense.Add((1-t)*(1-t)*a+2*(1-t)*t*c+t*t*b);}
        }
        Line(Points[Points.Length-1]);var result=new List<Vector2>{dense[0]};float station=1,total=0;
        for(int i=1;i<dense.Count;i++){float length=Vector2.Distance(dense[i-1],dense[i]);while(station<=total+length){result.Add(Vector2.Lerp(dense[i-1],dense[i],(station-total)/length));station+=1;}total+=length;}
        // Distribute the final residual over all samples: uniform spacing <= 1m.
        int count=Mathf.CeilToInt(total);result.Clear();result.Add(dense[0]);station=total/count;float stride=station;total=0;
        for(int i=1;i<dense.Count;i++){float length=Vector2.Distance(dense[i-1],dense[i]);while(result.Count<count&&station<=total+length){result.Add(Vector2.Lerp(dense[i-1],dense[i],(station-total)/length));station+=stride;}total+=length;}
        result.Add(dense[dense.Count-1]);return result.ToArray();
    }
    public static float CenterX(float z)
    {
        for(int i=0;i<SmoothPoints.Length-1;i++)
            if(z<=SmoothPoints[i+1].y)return Mathf.Lerp(SmoothPoints[i].x,SmoothPoints[i+1].x,Mathf.InverseLerp(SmoothPoints[i].y,SmoothPoints[i+1].y,z));
        return SmoothPoints[SmoothPoints.Length-1].x;
    }
    public static float DistanceToCenter(Vector3 p) {
        var q=new Vector2(p.x,p.z);float best=float.PositiveInfinity;
        for(int i=0;i<SmoothPoints.Length-1;i++) {
            var a=SmoothPoints[i];var d=SmoothPoints[i+1]-a;
            float t=Mathf.Clamp01(Vector2.Dot(q-a,d)/d.sqrMagnitude);
            best=Mathf.Min(best,Vector2.Distance(q,a+t*d));
        }
        return best;
    }
    public static Vector3 Point(int segment, float t, float offset=0)
    {
        Vector2 p=Vector2.Lerp(Points[segment],Points[segment+1],t);
        Vector2 d=(Points[segment+1]-Points[segment]).normalized;
        p+=new Vector2(d.y,-d.x)*offset;
        return new Vector3(p.x,0,p.y);
    }
    public static float Height(float z)
    {
        float h=Mathf.Tan(6*Mathf.Deg2Rad)*-40;
        if(z>=-40) return Mathf.Tan(6*Mathf.Deg2Rad)*z;
        for(int i=2;i>=0;i--)
        {
            float length=Vector2.Distance(Points[i],Points[i+1]);
            float t=Mathf.Clamp01((Points[i+1].y-z)/(Points[i+1].y-Points[i].y));
            h-=length*t*Mathf.Tan(Grades[i]*Mathf.Deg2Rad);
        }
        return h;
    }
    public static float Length
    {
        get { float length=0; for(int i=0;i<Points.Length-1;i++) {
            Vector3 a=Point(i,0),b=Point(i,1);a.y=Height(a.z);b.y=Height(b.z);
            length+=Vector3.Distance(a,b); } return length; }
    }
}
