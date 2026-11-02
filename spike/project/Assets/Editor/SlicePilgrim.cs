using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Rebuild the supplied production sheet; rectangles exclude headings, not character bounds.</summary>
public static class SlicePilgrim
{
    public const string AtlasPath = "Assets/Art/Character/pilgrim_atlas.png";
    const int CellW = 64, CellH = 80, Tolerance = 12;
    static readonly string[] Directions = { "down", "left", "right", "up" };

    [MenuItem("Mistbell/Slice Pilgrim")]
    public static void Run()
    {
        string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../art-ref/char-walk-sheet.jpg"));
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(sourcePath)) || source.width != 1280 || source.height != 960)
            throw new InvalidOperationException("Expected the supplied 1280x960 walk sheet");
        var atlas = new Texture2D(384, 320, TextureFormat.RGBA32, false);
        atlas.SetPixels32(new Color32[384 * 320]);
        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 6; col++)
        {
            // All 24 panels share these gutters. Frame numbers end above these content windows.
            int x0 = 220 + 173 * col, y0 = 99 + 213 * row;
            const int w = 158, h = 170;
            Color32 At(int x, int y) => source.GetPixel(x0 + x, source.height - 1 - y0 - y);
            var backgrounds = new[] { At(2,2), At(w-3,2), At(2,h-3), At(w-3,h-3) };
            var mask = new bool[w*h];
            for (int y=0; y<h; y++) for (int x=0; x<w; x++)
            {
                Color32 p = At(x,y);
                bool bg = false;
                foreach (var b in backgrounds)
                    if (Math.Abs(p.r-b.r)<=Tolerance && Math.Abs(p.g-b.g)<=Tolerance && Math.Abs(p.b-b.b)<=Tolerance) bg=true;
                // Printed mauve contact shadows are not part of the sprite. Only remove this
                // low-chroma ink in the foot strip; keep dark boots and cream staff ribbons.
                bool shadow = y > h * .83f && p.r > 100 && p.r-p.g < 32 && p.g-p.b < 16 && p.b >= p.g-16;
                mask[y*w+x] = !bg && !shadow;
            }
            // The main 8-connected silhouette excludes isolated paper/JPEG speckles and shadow islands.
            var seen = new bool[w*h];
            var largest = new List<int>();
            for (int seed=0; seed<mask.Length; seed++)
            {
                if (!mask[seed] || seen[seed]) continue;
                var part = new List<int>(); var queue = new Queue<int>();
                queue.Enqueue(seed); seen[seed]=true;
                while (queue.Count>0)
                {
                    int i=queue.Dequeue(); part.Add(i); int px=i%w, py=i/w;
                    for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=px+dx, ny=py+dy;
                        if(nx<0||nx>=w||ny<0||ny>=h) continue;
                        int n=ny*w+nx;
                        if(mask[n]&&!seen[n]) { seen[n]=true; queue.Enqueue(n); }
                    }
                }
                if (part.Count>largest.Count) largest=part;
            }
            if(largest.Count<100) throw new InvalidOperationException($"Empty source cell {row}/{col}");
            Array.Clear(mask,0,mask.Length);
            int minX=w,minY=h,maxX=0,maxY=0;
            foreach(int i in largest)
            {
                mask[i]=true; minX=Math.Min(minX,i%w); maxX=Math.Max(maxX,i%w);
                minY=Math.Min(minY,i/w); maxY=Math.Max(maxY,i/w);
            }
            int bw=maxX-minX+1,bh=maxY-minY+1;
            float scale=Math.Min(62f/bw,76f/bh);
            int dw=Mathf.RoundToInt(bw*scale),dh=Mathf.RoundToInt(bh*scale);
            int offsetX=(CellW-dw)/2;
            for(int y=0;y<dh;y++) for(int x=0;x<dw;x++)
            {
                int sx=minX+Math.Min(bw-1,Mathf.FloorToInt((x+.5f)*bw/dw));
                int sy=minY+Math.Min(bh-1,Mathf.FloorToInt((y+.5f)*bh/dh));
                if(mask[sy*w+sx]) atlas.SetPixel(col*CellW+offsetX+x,(3-row)*CellH+2+dh-1-y,At(sx,sy));
            }
        }
        int empty=0,baselineMin=80,baselineMax=0;
        for(int row=0;row<4;row++) for(int col=0;col<6;col++)
        {
            int bottom=80,top=-1;
            for(int y=0;y<80;y++) for(int x=0;x<64;x++)
                if(atlas.GetPixel(col*64+x,(3-row)*80+y).a>.5f) { bottom=Math.Min(bottom,y); top=Math.Max(top,y); }
            if(top<0) empty++;
            else { baselineMin=Math.Min(baselineMin,bottom);baselineMax=Math.Max(baselineMax,bottom); }
        }
        Debug.Log($"[PILGRIM] atlas=384x320 cells=24 emptyCells={empty} baseline_jitter={baselineMax-baselineMin}");
        if(empty!=0 || baselineMax-baselineMin>3 || baselineMin!=2)
            throw new InvalidOperationException("Pilgrim atlas acceptance failed");
        Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
        File.WriteAllBytes(AtlasPath,atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(AtlasPath,ImportAssetOptions.ForceUpdate);
        var importer=(TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;
        importer.isReadable=true; // Runtime acceptance projects the actual alpha bounds of every frame.
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.spritePixelsPerUnit=48;
        var sprites=new SpriteMetaData[24];
        for(int row=0;row<4;row++) for(int col=0;col<6;col++)
            sprites[row*6+col]=new SpriteMetaData { name=$"pilgrim_{Directions[row]}_{col}",
                rect=new Rect(col*64,(3-row)*80,64,80),alignment=(int)SpriteAlignment.Custom,pivot=new Vector2(.5f,2f/80f) };
#pragma warning disable 0618
        importer.spritesheet=sprites;
#pragma warning restore 0618
        importer.SaveAndReimport();
        UnityEngine.Object.DestroyImmediate(source);
        UnityEngine.Object.DestroyImmediate(atlas);
    }
}
