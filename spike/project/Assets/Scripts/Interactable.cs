using UnityEngine;

public sealed class Interactable : MonoBehaviour
{
    public PlayerController player;
    public InfoPanel panel;
    public PoiData data;
    public float radius = 3.5f;
    public bool InRange => data != null && player != null &&
        Vector3.Distance(player.transform.position, transform.position) <= radius;
    void Awake() { if (PlayerController.CaptureMode) enabled = false; }

    public void Configure(PoiData poi, PlayerController owner, InfoPanel info)
    {
        data = poi; player = owner; panel = info; radius = poi.radius;
        if (poi.id == "gergeti_p02")
        {
            var tower = GameObject.Find("ChurchCollision_BellTower").GetComponent<CapsuleCollider>();
            var center = tower.transform.TransformPoint(tower.center);
            var anchor = poi.anchor;
            // Only relocate an anchor inside the tower's horizontal footprint.
            if (Vector2.Distance(new Vector2(anchor.x, anchor.z), new Vector2(center.x, center.z)) < tower.radius)
                anchor = new Vector3(center.x, anchor.y, center.z - 2.4f);
            var support = GameObject.Find("Church Terrace").GetComponentsInChildren<Collider>();
            foreach (var c in support)
                if (c.name.StartsWith("TerraceFill_") && c.Raycast(
                    new Ray(new Vector3(anchor.x,500,anchor.z), Vector3.down), out var hit, 1000))
                    anchor.y = Mathf.Max(anchor.y, hit.point.y);
            poi.anchor = anchor;
            float intrusion = 0;
            foreach (var c in support)
            {
                if (!c.name.StartsWith("TerraceWall_")) continue;
                var b = c.bounds;
                float outside = Mathf.Max(b.min.x - 17, -17 - b.max.x,
                    b.min.z - 13.81f, -10.19f - b.max.z);
                intrusion = Mathf.Max(intrusion, -outside);
            }
            float clearance = Vector3.Distance(tower.ClosestPoint(anchor + Vector3.up), anchor + Vector3.up);
            Debug.Log(System.FormattableString.Invariant(
                $"[TOWER] collider=Capsule r={tower.radius:F1} h={tower.height:F0} wall_intrusion={intrusion:F3} poi_anchor=({anchor.x:F3},{anchor.y:F3},{anchor.z:F3}) poi_clearance={clearance:F3}"));
        }
        transform.position = poi.anchor;
        gameObject.name = "POI " + poi.id;
        var trigger = GetComponent<SphereCollider>();
        if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = radius;
    }

    public bool TryOpen()
    {
        if (!enabled || !InRange || panel.IsOpen || panel.ClosedFrame == Time.frameCount) return false;
        panel.Show(data);
        return true;
    }
}
