using System;
using UnityEngine;

// Read-only sightline audit. Query colliders copy rendered geometry and are never
// saved or used as gameplay obstacles. Existing wall colliders are also tested.
public static class AltarCrossVisibility
{
    public static void Check(ChurchInterior interior, Vector3 eye, Camera camera = null,
        float shiftZ = 0, float crossHeight = 1.10f, float lowerEdgeRaise = 0)
    {
        var filters = interior.detailRoot.GetComponentsInChildren<MeshFilter>(true);
        var altar = Array.Find(filters, f => f.name == "Interior_Altar_Model");
        float altarZ = altar.transform.position.z + shiftZ;
        float floor = interior.FloorY;
        var tip = new Vector3(-2, floor + 1.04f + crossHeight - .008f, altarZ + .29f);
        var ray = new Ray(eye, (tip - eye).normalized);
        float distance = Vector3.Distance(eye, tip) + .10f;
        float visualDistance = distance;
        string visualName = "none";
        Vector3 visualPoint = Vector3.zero;
        string hitName = "none";
        Vector3 hitPoint = Vector3.zero;
        bool crossHit = false;
        float bottom = float.PositiveInfinity;
        // Do not remove Interior_Iconostasis (or any other real blocker) from the ray.
        foreach (var hit in Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore))
            if (hit.distance < distance)
            {
                distance = hit.distance; hitName = hit.collider.name;
                hitPoint = hit.point; crossHit = false;
            }
        string nativeHit = hitName;
        bool royalDoor = Array.Exists(filters, f => f.name == "Interior_RoyalDoor_Frame");
        // Royal-door acceptance queries rendered geometry. Keep the native movement
        // ray result above in the log: the invisible obstacle intentionally remains.
        if (royalDoor) { distance = visualDistance; hitName = "none"; hitPoint = Vector3.zero; }
        foreach (var filter in filters)
        {
            bool isAltar = filter == altar;
            bool isWall = filter.name.StartsWith("Interior_V2NicheBack_") ||
                filter.name.StartsWith("Interior_ArchFrame_");
            if (royalDoor)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) continue;
                // Transparent shafts/pools transmit the sightline. Query the retained
                // source meshes once, rather than also intersecting their merged copy.
                if (filter.name.StartsWith("StaticArtBatch_") ||
                    Array.TrueForAll(renderer.sharedMaterials, m => m.renderQueue >= 2500)) continue;
            }
            else if (!isAltar && !isWall) continue;
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            var vertices = mesh.vertices;
            if (isWall)
            {
                float minimum = float.PositiveInfinity;
                foreach (var v in vertices) minimum = Mathf.Min(minimum, filter.transform.TransformPoint(v).y);
                for (int i = 0; i < vertices.Length; i++)
                    if (filter.transform.TransformPoint(vertices[i]).y < minimum + .025f)
                        vertices[i] += filter.transform.InverseTransformVector(Vector3.up * lowerEdgeRaise);
                if (filter.name == "Interior_V2NicheBack_1") bottom = minimum + lowerEdgeRaise;
            }
            else if (crossHeight != 1.10f)
            {
                // Only the cross occupies this narrow x/z prism; cloth/candles are outside it.
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = filter.transform.TransformPoint(vertices[i]);
                    if (Mathf.Abs(p.x + 2) > .18f || Mathf.Abs(p.z - altar.transform.position.z - .29f) > .045f || p.y < floor + 1.039f) continue;
                    p.y = floor + 1.04f + (p.y - floor - 1.04f) * crossHeight / 1.10f;
                    vertices[i] = filter.transform.InverseTransformPoint(p);
                }
            }
            mesh.vertices = vertices; mesh.RecalculateBounds();
            var query = new GameObject(filter.name + "_SightlineQuery");
            query.transform.SetPositionAndRotation(filter.transform.position + (isAltar ? Vector3.forward * shiftZ : Vector3.zero), filter.transform.rotation);
            query.transform.localScale = filter.transform.lossyScale;
            try
            {
                var collider = query.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                Physics.SyncTransforms();
                if (collider.Raycast(ray, out var hit, visualDistance))
                {
                    visualDistance = hit.distance; visualName = filter.name; visualPoint = hit.point;
                    if (hit.distance < distance)
                    {
                        distance = hit.distance; hitName = filter.name; hitPoint = hit.point;
                        crossHit = isAltar && hit.point.y > floor + 1.3f;
                    }
                }
            }
            finally
            {
                // Collider is disabled immediately, even when Destroy is deferred in a player.
                query.SetActive(false);
                if (Application.isPlaying) { UnityEngine.Object.Destroy(query); UnityEngine.Object.Destroy(mesh); }
                else { UnityEngine.Object.DestroyImmediate(query); UnityEngine.Object.DestroyImmediate(mesh); }
            }
        }
        string projected = camera == null ? "" : $" tip_screen={camera.WorldToScreenPoint(tip)} image={camera.pixelWidth}x{camera.pixelHeight}";
        Debug.Log(FormattableString.Invariant($"[ALTAR_CROSS] altar_z={altarZ:F4} cross_tip_y={tip.y:F4} wall_bottom_y={bottom:F4} visible_from_cam={crossHit.ToString().ToLowerInvariant()} hit={hitName} hit_y={hitPoint.y:F4} hit_z={hitPoint.z:F4} ray_mode={(royalDoor?"rendered_geometry":"movement_and_visual")} native_hit={nativeHit} visual_hit={visualName} visual_hit_y={visualPoint.y:F4} visual_hit_z={visualPoint.z:F4} camera=({eye.x:F4},{eye.y:F4},{eye.z:F4}) cross_height={crossHeight:F2} visual_lower_raise={lowerEdgeRaise:F2}{projected}"));
        if (royalDoor)
            Debug.Log(FormattableString.Invariant($"[ROYAL_DOOR_RAY] mode=rendered_geometry hit={visualName} native_hit={nativeHit} pass={crossHit.ToString().ToLowerInvariant()} camera={eye:F4}"));
        if (camera != null)
        {
            var a = camera.WorldToScreenPoint(new Vector3(-2.055f, tip.y - .22f, altarZ + .265f));
            var b = camera.WorldToScreenPoint(new Vector3(-1.945f, tip.y - .03f, altarZ + .265f));
            Debug.Log(FormattableString.Invariant($"[ALTAR_CROSS_ROI] x0={a.x:F3} y0={a.y:F3} x1={b.x:F3} y1={b.y:F3} origin=bottom_left image_w={camera.pixelWidth} image_h={camera.pixelHeight} region=upper_vertical_rod"));
        }
    }
}
