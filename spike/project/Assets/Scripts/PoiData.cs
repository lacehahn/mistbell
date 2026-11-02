using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PoiData
{
    public string id;
    public string titleCn;
    public string titleEn;
    public string bodyCn;
    public string bodyEn;
    public Vector3 anchor;
    public float radius;
}

[Serializable]
public class PoiDatabase
{
    public PoiData[] pois;

    public static PoiDatabase Load(MeshCollider ground)
    {
        var asset = Resources.Load<TextAsset>("PoiDatabase");
        if (asset == null) throw new InvalidOperationException("Missing Resources/PoiDatabase.json");
        var database = JsonUtility.FromJson<PoiDatabase>(asset.text);
        if (database == null || database.pois == null || database.pois.Length == 0)
            throw new InvalidOperationException("Empty POI database");
        var ids = new HashSet<string>();
        foreach (var data in database.pois)
        {
            if (data == null || string.IsNullOrEmpty(data.id) || !ids.Add(data.id) || data.radius <= 0 ||
                string.IsNullOrEmpty(data.titleCn) || string.IsNullOrEmpty(data.titleEn) ||
                string.IsNullOrEmpty(data.bodyCn) || string.IsNullOrEmpty(data.bodyEn))
                throw new InvalidOperationException("Invalid or duplicate POI");
            int lines = data.bodyCn.Split('\n').Length;
            if (lines < 4 || lines > 6) throw new InvalidOperationException("POI Chinese requires 4–6 explicit lines: " + data.id);
            if (!ground.Raycast(new Ray(new Vector3(data.anchor.x, 500, data.anchor.z), Vector3.down), out var hit, 1000))
                throw new InvalidOperationException("POI terrain snap failed: " + data.id);
            data.anchor.y = hit.point.y;
        }
        return database;
    }
}
