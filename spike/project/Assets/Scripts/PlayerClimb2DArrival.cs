using UnityEngine;

public sealed partial class PlayerController
{
    // Explicit handoff only. Keeps the existing outdoor lens, follow parameters, terrain and colliders.
    public bool ArriveFromClimb2D(out float groundError)
    {
        var target=stairDeck.bounds.center;
        if(!SurfaceAt(target,out var support)) { groundError=float.PositiveInfinity; return false; }
        target.y=support.point.y; transform.position=target; previousZ=target.z;
        velocity=Vector3.zero; cinemaDone=true; suppressCinema=true;
        follow.transform.position=target+FollowOffset;
        follow.transform.LookAt(target+Vector3.up*LookHeight);
        groundError=Mathf.Abs(transform.position.y-support.point.y);
        Debug.Log(System.FormattableString.Invariant($"[CLIMB2D] deck_support={support.collider.name} ground_error_m={groundError:F6}"));
        return (support.collider==stairDeck || support.collider.name.StartsWith("TerraceFill")) && groundError<=.02f;
    }
}
