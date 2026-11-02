using UnityEngine;

// Metres, shared by scene generation and runtime traversal checks.
public static class ClimbRoute
{
    public const float Width = 5f, Depth = .25f;
    public static readonly Vector3 Spawn = new Vector3(-18, 0, -205);
    public static readonly Vector2[] Points = {
        new Vector2(-18,-210), new Vector2(10,-150), new Vector2(-12,-95),
        new Vector2(0,-40), new Vector2(0,-13.25f) };
    static readonly float[] Grades = {6,8,10};
    public static float CenterX(float z)
    {
        for(int i=0;i<Points.Length-1;i++)
            if(z<=Points[i+1].y) return Mathf.Lerp(Points[i].x,Points[i+1].x,Mathf.InverseLerp(Points[i].y,Points[i+1].y,z));
        return 0;
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
