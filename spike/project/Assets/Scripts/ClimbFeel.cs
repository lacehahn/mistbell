using UnityEngine;

// One route-distance definition for build geometry, gameplay and acceptance captures.
public static class ClimbFeel
{
    public const int Flights=3, Steps=10;
    public const float Rise=.16f, Tread=.32f, Platform=2f, Shoulder=.15f;
    public static readonly float[] Stations=BuildStations();
    static float[] BuildStations() {
        var p=ClimbRoute.SmoothPoints; var s=new float[p.Length];
        for(int i=1;i<p.Length;i++) s[i]=s[i-1]+Vector2.Distance(p[i-1],p[i]);
        return s;
    }
    public static float Length=>Stations[Stations.Length-1];
    public static Vector2 Point(float station) {
        station=Mathf.Clamp(station,0,Length);
        int i=System.Array.BinarySearch(Stations,station);
        if(i>=0)return ClimbRoute.SmoothPoints[i];
        i=Mathf.Clamp(~i,1,Stations.Length-1);
        return Vector2.Lerp(ClimbRoute.SmoothPoints[i-1],ClimbRoute.SmoothPoints[i],Mathf.InverseLerp(Stations[i-1],Stations[i],station));
    }
    public static float Station(float z) {
        var p=ClimbRoute.SmoothPoints;
        for(int i=1;i<p.Length;i++)if(z<=p[i].y)return Mathf.Lerp(Stations[i-1],Stations[i],Mathf.InverseLerp(p[i-1].y,p[i].y,z));
        return Length;
    }
    public static float Progress(float z)=>Mathf.InverseLerp(Station(ClimbRoute.Spawn.z),Length,Station(z));
    public static float FlightStart(int flight)=>Length*(flight+1)/4f-Steps*Tread*.5f;
}
