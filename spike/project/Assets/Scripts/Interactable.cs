using UnityEngine;

public sealed class Interactable : MonoBehaviour
{
    public PlayerController player;
    public InfoPanel panel;
    public float radius = 3.5f;
    public bool InRange => Vector3.Distance(player.transform.position, transform.position) <= radius;
    void Awake() { if (PlayerController.CaptureMode) enabled = false; }
    void Update()
    {
        panel.SetPrompt(InRange && !panel.IsOpen);
        if (Input.GetKeyDown(KeyCode.E) && !panel.IsOpen && panel.ClosedFrame != Time.frameCount) TryOpen();
    }
    public bool TryOpen()
    {
        if (!InRange || panel.IsOpen) return false;
        panel.Open();
        return true;
    }
}
