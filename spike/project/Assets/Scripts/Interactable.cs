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
