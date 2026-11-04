using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// UV/texture-only update. Never regenerate geometry or call the older furnishing installer.
public static class AltarPixelIcons
{
    const string Folder="Assets/Art/Interior/Altar_Icons/";
    public static void Audit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        // Reapply in memory to verify idempotence and the imported native grid.
        // Do not save the scene or rebuild the player during this audit.
        Apply(UnityEngine.Object.FindFirstObjectByType<ChurchInterior>().detailRoot.transform);
        AltarBuild.Verify();
    }
    public static void Run()
    {
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var interior=UnityEngine.Object.FindFirstObjectByType<ChurchInterior>();
        Apply(interior.detailRoot.transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(interior.gameObject.scene);
        EditorSceneManager.SaveScene(interior.gameObject.scene);
        PerformanceBuild.Run();
    }

    public static void ApplyIfAvailable(Transform detail)
    {
        if(System.IO.File.Exists(Folder+"altar_icons_atlas.png")) Apply(detail);
    }

    static Texture2D Import(string name)
    {
        string path=Folder+name+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer==null)throw new InvalidOperationException("Generate pixel icons first: "+path);
        importer.textureType=TextureImporterType.Default;
        importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.crunchedCompression=false;importer.filterMode=FilterMode.Point;
        importer.mipmapEnabled=false;importer.anisoLevel=1;importer.wrapMode=TextureWrapMode.Clamp;
        importer.sRGBTexture=true;importer.isReadable=true;
        importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture.filterMode!=FilterMode.Point || texture.mipmapCount!=1 || texture.anisoLevel!=1)
            throw new InvalidOperationException("Pixel icon import settings failed");
        return texture;
    }

    public static void Apply(Transform detail)
    {
        foreach(string name in new[]{"icon_left","icon_mid","icon_right"})
        {
            var tex=Import(name);
            var colors=tex.GetPixels32();var palette=new System.Collections.Generic.HashSet<uint>();
            var logical=Import(name+"_logical");var cells=logical.GetPixels32();
            if(logical.width!=72 || logical.height!=110)throw new InvalidOperationException("Logical grid size changed");
            for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)
            {
                int sx=(int)((x+.5)*72/tex.width),sy=(int)((y+.5)*110/tex.height);
                if(!colors[y*tex.width+x].Equals(cells[sy*72+sx]))
                    throw new InvalidOperationException("Interpolated or irregular icon grid: "+name);
            }
            double lo=100,hi=0;int gold=0;
            foreach(var c in colors)
            {
                palette.Add((uint)(c.r<<16|c.g<<8|c.b));
                double Linear(byte b){double v=b/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
                double y=.2126*Linear(c.r)+.7152*Linear(c.g)+.0722*Linear(c.b);
                double l=116*(y>Math.Pow(6.0/29,3)?Math.Pow(y,1.0/3):y/(3*Math.Pow(6.0/29,2))+4.0/29)-16;
                lo=Math.Min(lo,l);hi=Math.Max(hi,l);
                if(c.r==201 && c.g==162 && c.b==39)gold++;
            }
            double pct=gold*100.0/colors.Length;
            if(tex.width!=512 || tex.height!=784 || palette.Count>12 || lo<25 || hi>70 || pct>15)
                throw new InvalidOperationException("Pixel palette acceptance failed: "+name);
            Debug.Log(FormattableString.Invariant($"[ALTAR_ICONS] icon={name} px_grid=72x110 palette_max={palette.Count} luma_min={lo:F3} luma_max={hi:F3} gold_pct={pct:F3} filter=point no_mipmap=true aniso=1 compression=none size={tex.width}x{tex.height} grid_ok=true"));
        }
        var atlas=Import("altar_icons_atlas");
        var emission=Import("altar_icons_emission");
        var mf=Array.Find(detail.GetComponentsInChildren<MeshFilter>(true),f=>f.name=="Interior_Altar_Model");
        if(mf==null)throw new InvalidOperationException("Existing altar not found");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Church/gergeti_altar.fbx");
        var source=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name="AltarPixelIconsUV";
        var uv=source.uv;var vertices=source.vertices;var count=new int[3];
        float floor=UnityEngine.Object.FindFirstObjectByType<ChurchInterior>().FloorY;
        for(int j=0;j<uv.Length;j++)
        {
            var old=uv[j];uv[j]=old*.5f;
            // Several wood charts overlap the old icon island; identify the physical
            // inset boards as well, without changing their geometry or the gold rim.
            if(old.x<.8009f || old.x>.9231f || old.y<.7029f || old.y>.8831f)continue;
            var world=mf.transform.TransformPoint(vertices[j]);
            if(world.y<floor+.2399f || world.y>floor+.6601f || world.z<8.3239f || world.z>8.3281f)continue;
            int k=Mathf.RoundToInt((world.x+2)/.66f)+1;
            if(k<0 || k>2)throw new InvalidOperationException("Icon index outside expected panels");
            if(Mathf.Abs(world.x-(-2+(k-1)*.66f))>.1301f)continue;
            float s=Mathf.Clamp01((old.x-.801f)/.122f),t=Mathf.Clamp01((old.y-.703f)/.180f);
            uv[j]=new Vector2((k*512+.5f+s*511)/2048,(1264.5f+t*783)/2048);
            count[k]++;
        }
        foreach(int n in count)if(n!=24)throw new InvalidOperationException("Expected 24 painting vertices per panel, got "+string.Join("/",count));
        mesh.uv=uv;
        // Source geometry must match the existing authored altar, including every vertex/index.
        var before=mf.sharedMesh;
        if(before.vertexCount!=mesh.vertexCount || before.subMeshCount!=mesh.subMeshCount)
            throw new InvalidOperationException("Authored/source geometry differs");
        var prior=before.vertices;
        for(int j=0;j<prior.Length;j++)if(prior[j]!=vertices[j])throw new InvalidOperationException("Vertex changed");
        for(int k=0;k<mesh.subMeshCount;k++)
            if(!System.Linq.Enumerable.SequenceEqual(before.GetTriangles(k),mesh.GetTriangles(k)))throw new InvalidOperationException("Indices changed");
        const string meshPath=Folder+"AltarPixelIconsUV.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing==null){AssetDatabase.CreateAsset(mesh,meshPath);existing=mesh;}
        else {EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);}
        mf.sharedMesh=existing;EditorUtility.SetDirty(mf);
        var renderer=mf.GetComponent<MeshRenderer>();
        if(renderer.sharedMaterials.Length!=2)throw new InvalidOperationException("Expected existing two material slots");
        foreach(var mat in renderer.sharedMaterials)
        {
            mat.SetTexture("_BaseMap",atlas);
            // The old Lit material had _EMISSION disabled. Use a black mask outside
            // the paintings, so readability bounce cannot brighten rims or stone.
            if(mat.name=="M_AltarPaintedRecess")
            {
                mat.SetTexture("_EmissionMap",emission);mat.SetColor("_EmissionColor",Color.white*.7f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
            }
            if(mat.HasProperty("_AltarUVScale"))mat.SetFloat("_AltarUVScale",2);
            EditorUtility.SetDirty(mat);
        }
        int tris=existing.triangles.Length/3;
        if(tris>9000)throw new InvalidOperationException("Triangle budget exceeded");
        Debug.Log($"[ALTAR_ICONS] grid_ok=true mapping=verified_by_altar_icons_pixel.py geometry_unchanged=true icon_vertices={string.Join("/",count)} tris={tris} materials={renderer.sharedMaterials.Length} filter=point no_mipmap=true atlas=2048x2048 icon_bounce=0.7 emission_mask=icons_only");
    }
}
