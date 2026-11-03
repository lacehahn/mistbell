using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Geometry/material-preserving batches. Originals remain for collision and diagnostic queries.
public static class StaticArtBatching
{
    struct Key : IEquatable<Key>
    {
        public Transform owner;
        public int x,z,layer;
        public ShadowCastingMode shadows;
        public bool receive;
        public uint mask;
        public bool Equals(Key b) => owner==b.owner && x==b.x && z==b.z && layer==b.layer && shadows==b.shadows && receive==b.receive && mask==b.mask;
        public override bool Equals(object b) => b is Key k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(owner,x,z,layer,shadows,receive,mask);
    }

    static bool SameProperties(Material a,Material b)
    {
        if(a.shader!=b.shader || a.renderQueue!=b.renderQueue || a.enableInstancing!=b.enableInstancing ||
           a.doubleSidedGI!=b.doubleSidedGI || a.globalIlluminationFlags!=b.globalIlluminationFlags) return false;
        var ak=a.shaderKeywords; var bk=b.shaderKeywords; Array.Sort(ak); Array.Sort(bk);
        if(string.Join(";",ak)!=string.Join(";",bk)) return false;
        for(int i=0;i<a.passCount;i++) if(a.GetShaderPassEnabled(a.GetPassName(i))!=b.GetShaderPassEnabled(b.GetPassName(i))) return false;
        for(int i=0;i<a.shader.GetPropertyCount();i++)
        {
            int id=a.shader.GetPropertyNameId(i);
            switch(a.shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Color: if(!a.GetColor(id).Equals(b.GetColor(id))) return false; break;
                case ShaderPropertyType.Vector: if(!a.GetVector(id).Equals(b.GetVector(id))) return false; break;
                case ShaderPropertyType.Texture:
                    if(a.GetTexture(id)!=b.GetTexture(id) || a.GetTextureScale(id)!=b.GetTextureScale(id) || a.GetTextureOffset(id)!=b.GetTextureOffset(id)) return false; break;
                case ShaderPropertyType.Int: if(a.GetInteger(id)!=b.GetInteger(id)) return false; break;
                default: if(a.GetFloat(id)!=b.GetFloat(id)) return false; break;
            }
        }
        return true;
    }

    static void BatchPews(ChurchInterior interior)
    {
        if(interior==null || interior.pews==null || interior.pews.Length<2) return;
        var outdoor=interior.pewOutdoorMaterials[0];
        if(!Array.TrueForAll(interior.pewOutdoorMaterials,m=>m==outdoor)) return;
        var slots=new List<CombineInstance>(); long expected=0;
        for(int slot=0;slot<2;slot++)
        {
            var parts=new List<CombineInstance>();
            foreach(var r in interior.pews)
            {
                var f=r.GetComponent<MeshFilter>();
                parts.Add(new CombineInstance { mesh=f.sharedMesh,subMeshIndex=slot,transform=interior.transform.worldToLocalMatrix*f.transform.localToWorldMatrix });
                expected+=(long)f.sharedMesh.GetIndexCount(slot)/3;
            }
            var mesh=new Mesh {indexFormat=IndexFormat.UInt32}; mesh.CombineMeshes(parts.ToArray(),true,true);
            slots.Add(new CombineInstance {mesh=mesh,transform=Matrix4x4.identity});
        }
        var combined=new Mesh {name="StaticPewBatch",indexFormat=IndexFormat.UInt32};
        combined.CombineMeshes(slots.ToArray(),false,false); combined.RecalculateBounds();
        long actual=0; for(int i=0;i<2;i++) actual+=(long)combined.GetIndexCount(i)/3;
        if(actual!=expected) throw new InvalidOperationException("Pew batching changed triangle count");
        foreach(var slot in slots) UnityEngine.Object.Destroy(slot.mesh);
        var go=new GameObject("StaticPewBatch"); go.transform.SetParent(interior.transform,false);
        go.layer=interior.pews[0].gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh=combined;
        var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterials=new[]{outdoor,outdoor};
        renderer.shadowCastingMode=interior.pews[0].shadowCastingMode; renderer.receiveShadows=interior.pews[0].receiveShadows;
        renderer.renderingLayerMask=interior.pews[0].renderingLayerMask;
        interior.pewBatch=renderer;
        foreach(var r in interior.pews) r.forceRenderingOff=true;
        Debug.Log($"[STATIC_PEW_BATCH] sources={interior.pews.Length} meshes=1 source_tris={expected} merged_tris={actual} material_slots=2");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bench-no-batching")>=0) return;
        var interior=UnityEngine.Object.FindFirstObjectByType<ChurchInterior>();
        var sources=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var groups=new Dictionary<Key,List<MeshRenderer>>();
        Material footprintMaterial=null;
        var slabMaterialGroups=new Dictionary<Material,int>();
        foreach(var r in sources)
        {
            if(!r.enabled || r.isPartOfStaticBatch || r.forceRenderingOff || r.HasPropertyBlock() || r.lightmapIndex>=0 && r.lightmapIndex<65534) continue;
            var filter=r.GetComponent<MeshFilter>();
            if(filter==null || filter.sharedMesh==null || !filter.sharedMesh.isReadable) continue;
            if(interior!=null && interior.drumCutFilters!=null && Array.Exists(interior.drumCutFilters,f=>f==filter)) continue;
            var mats=r.sharedMaterials;
            if(mats.Length!=filter.sharedMesh.subMeshCount || Array.Exists(mats,m=>m==null || m.renderQueue>=2500)) continue;
            // Footprint assets were 176 byte-identical material clones with different names.
            // Share only after comparing every shader property and render keyword.
            if(r.transform.root.name=="Snow Footprints" && mats.Length==1)
            {
                if(footprintMaterial==null) footprintMaterial=mats[0];
                else if(SameProperties(footprintMaterial,mats[0])) { r.sharedMaterial=footprintMaterial; mats[0]=footprintMaterial; }
            }
            Transform owner=null;
            if(interior!=null && r.transform.IsChildOf(interior.detailRoot.transform)) owner=interior.detailRoot.transform;
            else
            {
                var root=r.transform.root;
                if(root.name=="Props" || root.name=="Stone Path" || root.name=="Church Terrace" ||
                   root.name=="Church Snow Contact" || root.name=="Snow2 Surface Details" || root.name=="Snow Footprints" || root.name=="Winter Warm Lights") owner=root;
                if(interior!=null && root==interior.transform && r!=interior.shell && r!=interior.legacyChandelier &&
                   r.gameObject!=interior.floor.gameObject && !r.transform.IsChildOf(interior.doorLeaf) &&
                   !Array.Exists(interior.pews,p=>p==r) && !Array.Exists(interior.shafts,p=>p==r)) owner=root;
            }
            if(owner==null) continue; // Dynamic door, shell materials, pews, shafts, particles, terrain and backdrop stay independent.
            // Four floor meshes partition materials, so each material is submitted only once per pass.
            bool slab=r.name.StartsWith("Interior_FloorSlab_",StringComparison.Ordinal);
            int slabGroup=0;
            if(slab && !slabMaterialGroups.TryGetValue(mats[0],out slabGroup))
            { slabGroup=slabMaterialGroups.Count%4; slabMaterialGroups.Add(mats[0],slabGroup); }
            var key=new Key { owner=owner, x=slab?10000:0,
                z=slab?slabGroup:0,
                layer=r.gameObject.layer,shadows=r.shadowCastingMode,receive=r.receiveShadows,mask=r.renderingLayerMask };
            if(!groups.TryGetValue(key,out var list)) groups[key]=list=new List<MeshRenderer>();
            list.Add(r);
        }
        int count=0,batches=0,floorGroups=0; long beforeTris=0,afterTris=0;
        foreach(var group in groups)
        {
            if(group.Value.Count<2) continue;
            var key=group.Key;
            if(key.x==10000) floorGroups++;
            var byMaterial=new Dictionary<Material,List<CombineInstance>>();
            long expected=0;
            foreach(var r in group.Value)
            {
                var f=r.GetComponent<MeshFilter>(); var mats=r.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    if(!byMaterial.TryGetValue(mats[i],out var list)) byMaterial[mats[i]]=list=new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh=f.sharedMesh,subMeshIndex=i, transform=key.owner.worldToLocalMatrix*f.transform.localToWorldMatrix });
                    expected+=(long)f.sharedMesh.GetIndexCount(i)/3;
                }
            }
            var materials=new List<Material>(); var merged=new List<CombineInstance>();
            foreach(var material in byMaterial)
            {
                var part=new Mesh { indexFormat=IndexFormat.UInt32 };
                part.CombineMeshes(material.Value.ToArray(),true,true);
                materials.Add(material.Key); merged.Add(new CombineInstance { mesh=part,transform=Matrix4x4.identity });
            }
            var mesh=new Mesh { name="StaticArtBatch",indexFormat=IndexFormat.UInt32 };
            mesh.CombineMeshes(merged.ToArray(),false,false); mesh.RecalculateBounds();
            long actual=0;
            for(int i=0;i<mesh.subMeshCount;i++) actual+=(long)mesh.GetIndexCount(i)/3;
            if(actual!=expected) throw new InvalidOperationException("Static batching changed triangle count");
            foreach(var part in merged) UnityEngine.Object.Destroy(part.mesh);
            var go=new GameObject("StaticArtBatch_"+batches++); go.layer=key.layer; go.transform.SetParent(key.owner,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterials=materials.ToArray();
            renderer.shadowCastingMode=key.shadows; renderer.receiveShadows=key.receive; renderer.renderingLayerMask=key.mask;
            // Originals keep their enabled state, transforms, bounds, mesh and collider references.
            foreach(var r in group.Value) r.forceRenderingOff=true;
            count+=group.Value.Count; beforeTris+=expected; afterTris+=actual;
        }
        BatchPews(interior);
        Debug.Log($"[STATIC_BATCH] sources={count} meshes={batches} source_tris={beforeTris} merged_tris={afterTris} grouping=root_material floor_groups={floorGroups}");
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bench")>=0)
        {
            var remaining=new Dictionary<string,(int count,long tris)>();
            foreach(var r in sources)
            {
                if(r.forceRenderingOff || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var f=r.GetComponent<MeshFilter>(); if(f==null || f.sharedMesh==null) continue;
                string root=r.transform.root.name; remaining.TryGetValue(root,out var stat);
                stat.count++; for(int i=0;i<f.sharedMesh.subMeshCount;i++) stat.tris+=(long)f.sharedMesh.GetIndexCount(i)/3;
                remaining[root]=stat;
            }
            foreach(var stat in remaining) Debug.Log($"[STATIC_REMAINING] root={stat.Key} renderers={stat.Value.count} tris={stat.Value.tris}");
        }
    }
}
