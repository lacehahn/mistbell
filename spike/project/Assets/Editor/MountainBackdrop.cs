using System;
using System.IO;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Deterministic painted background. Does not consume UnityEngine.Random state.
public static class MountainBackdrop
{
    const int W = 2048, H = 1024;
    static float Hash(int n)
    {
        unchecked { uint v=(uint)n; v^=v>>16; v*=0x7feb352d; v^=v>>15; v*=0x846ca68b; v^=v>>16; return (v&0xffffff)/16777215f; }
    }
    static float Noise(float x, int seed)
    {
        int i=Mathf.FloorToInt(x); float t=x-i;
        t=t*t*(3-2*t);
        return Mathf.Lerp(Hash(i+seed)*2-1,Hash(i+1+seed)*2-1,t);
    }
    static float Noise2(float x,float y,int seed)
    {
        int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
        float tx=x-ix,ty=y-iy;
        tx=tx*tx*(3-2*tx); ty=ty*ty*(3-2*ty);
        return Mathf.Lerp(Mathf.Lerp(Hash(seed+ix+iy*7919),Hash(seed+ix+1+iy*7919),tx),
            Mathf.Lerp(Hash(seed+ix+(iy+1)*7919),Hash(seed+ix+1+(iy+1)*7919),tx),ty)*2-1;
    }
    static Color Hex(string s) { ColorUtility.TryParseHtmlString(s,out var c); return c; }
    static readonly int[] Seeds = { 15731, 80447, 213397, 379391 };
    const float HeightScale = 1.15f * .92f;
    const float Overscan = .12f, Feather = .08f;
    const float FootFadePct = .18f;
    // Preserve the previous cards' feet; scaling is anchored here, never at the crest.
    static readonly int[] OriginalCrests = { 888, 920, 896, 904 };
    static float maxPeakStdPct;
    static readonly float[] LayerLuma = new float[4];
    static readonly int[] RockStreaks = new int[3], SnowGullies = new int[3], PeakCounts = new int[4];
    static readonly int[] SpurCounts = new int[4];
    static readonly float[] SpurContrast = new float[4], GullyContrast = new float[4];
    struct Branch
    {
        public float x, top, length, lean, width;
        public float bendA,bendB,bendC, knotA,knotB,knotC;
        public float Bend(float t)
        {
            float a,b,u;
            if(t<knotA) { a=0;b=bendA;u=t/knotA; }
            else if(t<knotB) { a=bendA;b=bendB;u=(t-knotA)/(knotB-knotA); }
            else if(t<knotC) { a=bendB;b=bendC;u=(t-knotB)/(knotC-knotB); }
            else { a=bendC;b=0;u=(t-knotC)/(1-knotC); }
            return Mathf.Lerp(a,b,Mathf.SmoothStep(0,1,u));
        }
        // Distances/widths are in output pixels, rather than stretched texture texels.
        public float Mask(int px, int py, Vector2 scale, out float along)
        {
            float dy=(top-py)*scale.y;
            along=dy*Mathf.Sqrt(1+lean*lean);
            if(along<0 || along>=length) return 0;
            float distance=Mathf.Abs((px-x)*scale.x-dy*lean-Bend(along/length))/Mathf.Sqrt(1+lean*lean);
            float edge=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(width*.15f,width*.5f,distance));
            float tip=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,along/length));
            return edge*tip;
        }
    }
    // Snow is independent of the multi-bend rock relief. Screen-space widths
    // include a one-pixel feather on each side; the centerline turns only once.
    struct SnowGully
    {
        public int summit;
        public float x, top, length, width, bend;
        public float Center(float t) => x + (t < .55f
            ? Mathf.Lerp(0,bend,t/.55f) : Mathf.Lerp(bend,bend*.4f,(t-.55f)/.45f));
    }
    static float maxSnowDepth, minSnowWidth, maxSnowWidth;
    static int maxSnowReversals;
    static int minCap, maxCap;
    static float minFaceDelta;
    const int RockLevels = 3;
    static float maxFootJump;
    const float ShadowRatio = .70f, FacetSize = 16f;
    static int ridgeBreaks, straightSegments, maxFacePixels;
    static float minShadowRatio, minShadeLuma, maxFaceGap;
    static float Luma(Color c) => .2126f*c.r+.7152f*c.g+.0722f*c.b;
    // Monotone cubic interpolation keeps the original peak count while rounding
    // every secondary bend. Coordinates are continuous until the 2x resolve.
    static float Flank(float x, float[] xs, float[] ys)
    {
        int i=0; while(i<xs.Length-2 && x>xs[i+1]) i++;
        float span=xs[i+1]-xs[i], t=Mathf.Clamp01((x-xs[i])/span);
        float slope=(ys[i+1]-ys[i])/span;
        float a=i==0?slope:(ys[i]-ys[i-1])/(xs[i]-xs[i-1]);
        float b=i==xs.Length-2?slope:(ys[i+2]-ys[i+1])/(xs[i+2]-xs[i+1]);
        float m0=a*slope<=0?0:2*a*slope/(a+slope);
        float m1=b*slope<=0?0:2*b*slope/(b+slope);
        return (2*t*t*t-3*t*t+1)*ys[i]+(t*t*t-2*t*t+t)*span*m0
            +(-2*t*t*t+3*t*t)*ys[i+1]+(t*t*t-t*t)*span*m1;
    }
    static float[] FlankKnots(float start,float end,float low,float high,int seed,Vector2 scale,out float[] heights)
    {
        var xs=new float[5]; heights=new float[5];
        xs[0]=start; xs[4]=end; heights[0]=low; heights[4]=high;
        for(int k=1;k<=3;k++)
        {
            float sign=Hash(seed+k*29)>.5f?1:-1;
            float dx=sign*Mathf.Lerp(6,18,Hash(seed+k*41))/scale.x;
            xs[k]=Mathf.Lerp(start,end,k*.25f)+dx;
            xs[k]=Mathf.Clamp(xs[k],Mathf.Lerp(start,end,(k-.35f)*.25f),Mathf.Lerp(start,end,(k+.35f)*.25f));
            float dy=(Hash(seed+k*53)>.5f?1:-1)*Mathf.Lerp(3,8,Hash(seed+k*67))/scale.y;
            heights[k]=Mathf.Lerp(low,high,(xs[k]-start)/(end-start))+dy;
            // A narrow projected flank cannot contain the full displacement.
            heights[k]=Mathf.Clamp(heights[k],Mathf.Lerp(low,high,(k-.4f)*.25f),Mathf.Lerp(low,high,(k+.4f)*.25f));
        }
        for(int k=1;k<=3;k++)
        {
            float a=(heights[k]-heights[k-1])/(xs[k]-xs[k-1]);
            float b=(heights[k+1]-heights[k])/(xs[k+1]-xs[k]);
            if(Mathf.Abs(a-b)>1e-5f) ridgeBreaks++; else straightSegments++;
        }
        return xs;
    }
    // Shared vertices partition every broad face into continuous triangular
    // patches of 128 output px². Adjacent patches have no brightness jump;
    // the complete transition range is 0.04 before atmospheric perspective.
    static float Facet(float x,float y,int seed)
    {
        float gx=x/FacetSize, gy=y/FacetSize;
        int ix=Mathf.FloorToInt(gx), iy=Mathf.FloorToInt(gy);
        float u=gx-ix,v=gy-iy;
        float a=Hash(seed+ix*173+iy*7919), b=Hash(seed+(ix+1)*173+iy*7919);
        float c=Hash(seed+ix*173+(iy+1)*7919), d=Hash(seed+(ix+1)*173+(iy+1)*7919);
        float value=u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
        maxFacePixels=Math.Max(maxFacePixels,Mathf.CeilToInt(FacetSize*FacetSize*.5f));
        return (value-.5f)*.04f;
    }
    static float[] Profile(int layer, Vector2 pixelScale, out int peaks, out float std)
    {
        var rng = new System.Random(Seeds[layer]);
        peaks = 9 + layer;
        var ridge = new float[W];
        var widths = new float[peaks];
        float total = 0;
        for (int i=0;i<peaks;i++) { widths[i]=60+rng.Next(165); total+=widths[i]; }
        int start=0;
        float valley=OriginalCrests[layer]-150;
        // All crowns and shoulders now use continuous cubic curves.
        int rounded=peaks;
        for(int i=0;i<peaks;i++)
        {
            int end=i==peaks-1?W-1:start+Mathf.RoundToInt(widths[i]/total*(W-1));
            float apex=Mathf.Lerp(start,end,.36f+(float)rng.NextDouble()*.27f);
            float top=OriginalCrests[layer]-2*rng.Next(0,24);
            float nextValley=OriginalCrests[layer]-2*rng.Next(48,95);
            float[] leftY,rightY;
            var leftX=FlankKnots(start,apex,valley,top,Seeds[layer]+i*101,pixelScale,out leftY);
            // Reflect the descending flank to keep the monotone interpolator increasing.
            var rightX=FlankKnots(0,end-apex,nextValley,top,Seeds[layer]+i*101+509,pixelScale,out rightY);
            for(int x=start;x<=end;x++)
            {
                float ridgeSum=0;
                for(int sample=0;sample<2;sample++)
                {
                    float sx=Mathf.Clamp(x+(sample==0?-.25f:.25f),start,end);
                    ridgeSum+=sx<=apex?Flank(sx,leftX,leftY):Flank(end-sx,rightX,rightY);
                }
                ridge[x]=ridgeSum*.5f;
            }
            start=end; valley=nextValley;
        }
        // Measure actual raster maxima, not requested/random apex parameters.
        var heights=new System.Collections.Generic.List<float>();
        int direction=0;
        for(int x=1;x<W;x++)
        {
            int d=Math.Sign(ridge[x]-ridge[x-1]);
            if(d==0) continue;
            if(direction>0 && d<0) heights.Add(ridge[x-1]);
            direction=d;
        }
        if(heights.Count!=peaks) throw new InvalidOperationException("MTN7 raster peak count");
        float sum=0, sum2=0;
        foreach(float h in heights) {sum+=h;sum2+=h*h;}
        float mean=sum/heights.Count;
        std=Mathf.Sqrt(Mathf.Max(0,sum2/heights.Count-mean*mean));
        float pct=100*std/mean;
        maxPeakStdPct=Mathf.Max(maxPeakStdPct,pct);
        if(pct>20) throw new InvalidOperationException("MTN7 peak height variation gate");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN7_PROFILE] L{0} peaks={1} rounded={2} peak_height_std_pct={3:F4}",layer+1,heights.Count,rounded,pct));
        return ridge;
    }
    // Irregular knot spacing gives two genuinely distinct serration scales, without
    // a fixed repeating sawtooth. Neither helper touches the scene random generator.
    static float Jagged(int x,int seed,int minPeriod,int maxPeriod)
    {
        int start=0,index=0;
        while(true)
        {
            int period=minPeriod+Mathf.FloorToInt(Hash(seed+index*31)*(maxPeriod-minPeriod+1));
            period=Mathf.Min(period,maxPeriod);
            if(x<start+period)
                return Mathf.Lerp(Hash(seed+index*47+701),Hash(seed+(index+1)*47+701),(x-start)/(float)period)*2-1;
            start+=period; index++;
        }
    }
    static float Luma(Color32 p) => (.2126f*p.r+.7152f*p.g+.0722f*p.b)/255f;
    struct Stroke
    {
        public int x, length, width;
        public float top;
        public float lean, contrast;
        public float Center(int y) => x+(top-y)*lean;
    }
    static Color Rock(Color basis, bool lit, float mix, Color air)
    {
        // Snow bounce lifts the shadow into neutral grey-blue. Keep the entire
        // unsnowed mountain's face range below .26, including different rock beds.
        float target=Mathf.Lerp(.65f,.67f,Mathf.InverseLerp(.43f,.57f,Luma(basis)));
        Color shade=Hex("#5F6E80");
        shade*=target*ShadowRatio/Luma(shade); shade.a=1;
        Color light=basis; float lift=target-Luma(light);
        light.r+=lift; light.g+=lift; light.b+=lift;
        return Color.Lerp(lit?light:shade,air,mix);
    }
    static Texture2D Paint(int layer,float[] ridge,float visibleFoot,Vector2 pixelScale)
    {
        var pixels=new Color32[W*H];
        Color[] rock={Hex("#66717F"),Hex("#77828F"),Hex("#88939F")};   // 原色在阴天光下读成近黑
        Color gullySnow=Hex("#DCE8F2");
        Color snow=Hex("#E4EEF7"), transition=Hex("#C9D7E5");   // 纯白 #F2F7FB 让山变成剪纸
        Color haze=Hex("#EDF3F8"), air=layer==3?Hex("#D6E2EC"):Hex("#DAE5EF");
        float mix=layer==0?0:layer==1?.38f:layer==2?.68f:.75f;
        int seed=Seeds[layer]; float crest=0;
        foreach(float h in ridge) crest=Math.Max(crest,h);
        float hazeHeight=(crest-visibleFoot)*.12f;
        // The card extends below the snowfield. Fade from its visible foot,
        // rather than wasting the fade in the buried UV rows. Full UV 0..1 is retained.
        float fadeTop=visibleFoot+H*FootFadePct;
        var caps=new float[W];
        var summits=new System.Collections.Generic.List<int>();
        int direction=0;
        for(int x=1;x<W;x++)
        {
            int d=Math.Sign(ridge[x]-ridge[x-1]);
            if(d==0) continue;
            if(direction>0 && d<0) summits.Add(x-1);
            direction=d;
        }
        // Valley-to-valley ownership: every original peak keeps its own lit left face.
        var apex=new int[W];
        int start=0;
        for(int i=0;i<summits.Count;i++)
        {
            int end=W-1;
            if(i+1<summits.Count)
            {
                end=summits[i];
                for(int x=summits[i];x<summits[i+1];x++) if(ridge[x]<ridge[end]) end=x;
            }
            for(int x=start;x<=end;x++) apex[x]=summits[i];
            start=end+1;
        }
        // Smooth both apex and valley face changes; binary ownership made full-height seams.
        var faceLight=new float[W];
        for(int x=0;x<W;x++)
        {
            float sum=0,weight=0;
            for(int dx=-32;dx<=32;dx++)
            {
                int sample=Mathf.Clamp(x+dx,0,W-1);
                float w=33-Math.Abs(dx);
                sum+=(sample<=apex[sample]?1:0)*w; weight+=w;
            }
            faceLight[x]=sum/weight;
        }
        for(int x=0;x<W;x++)
        {
            int l=Mathf.Max(0,x-8),r=Mathf.Min(W-1,x+8);
            float slope=Mathf.Abs((ridge[r]-ridge[l])/(float)Mathf.Max(1,r-l));
            float deposit=Mathf.Clamp01(.5f+.5f*Jagged(x,seed+503,12,24)+.25f*Jagged(x,seed+907,4,8));
            float steep=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1.1f,slope));
            caps[x]=2f*(Mathf.Lerp(Mathf.Lerp(12,24,deposit),Mathf.Lerp(5,8,deposit),steep));
            if(layer<3) { minCap=Math.Min(minCap,Mathf.RoundToInt(caps[x]/2)); maxCap=Math.Max(maxCap,Mathf.RoundToInt(caps[x]/2)); }
        }
        int streakCount=layer==0?12:layer==1?14:10;
        var streaks=new Stroke[layer==3?0:streakCount];
        var gullies=new System.Collections.Generic.List<SnowGully>();
        var streakDrawn=new bool[streaks.Length];
        for(int i=0;i<streaks.Length;i++)
        {
            int x=Mathf.RoundToInt((i+.25f+.5f*Hash(seed+i*53))*W/streaks.Length);
            float side=x<apex[x]?-1:1;
            streaks[i]=new Stroke { x=x,top=ridge[x]-caps[x]-6,
                length=Mathf.RoundToInt((ridge[x]-visibleFoot)*Mathf.Lerp(.45f,.9f,Hash(seed+i*71))),
                width=Mathf.Max(4,Mathf.CeilToInt(Mathf.Lerp(4,8,Hash(seed+i*97))/pixelScale.x)),lean=side*Mathf.Tan(Mathf.Lerp(2,8,Hash(seed+i*113))*Mathf.Deg2Rad),
                contrast=Mathf.Lerp(.94f,1f,Hash(seed+i*149)) };
        }
        if(layer<3)
            foreach(int summit in summits)
            {
                int end=summit;
                while(end+1<W && apex[end+1]==summit) end++;
                for(int i=0;i<2;i++)
                {
                    int x=Mathf.RoundToInt(Mathf.Lerp(summit,end,i==0?.23f:.53f));
                    if(faceLight[x]>.05f || (end-x)*pixelScale.x<10) continue;
                    float top=ridge[x]-caps[x]+2;
                    float length=Mathf.Min((ridge[x]-visibleFoot)*.32f,top-fadeTop);
                    if(length*pixelScale.y<8) continue;
                    float width=3+Mathf.Floor(Hash(seed+summit*389+i*97)*4.999f);
                    var g=new SnowGully {summit=summit,x=x*pixelScale.x,top=top,
                        length=length,width=width,bend=Mathf.Lerp(12,24,Hash(seed+summit*397+i*113))};
                    int reversals=0, previous=0;
                    float last=g.Center(0);
                    for(int k=1;k<=100;k++)
                    {
                        float next=g.Center(k/100f); int sign=Math.Sign(next-last);
                        if(sign!=0) { if(previous!=0 && previous!=sign) reversals++; previous=sign; }
                        last=next;
                    }
                    maxSnowReversals=Math.Max(maxSnowReversals,reversals);
                    minSnowWidth=Mathf.Min(minSnowWidth,width); maxSnowWidth=Mathf.Max(maxSnowWidth,width);
                    gullies.Add(g);
                }
            }
        var gullyDrawn=new bool[gullies.Count];
        // Two or three subsidiary ridges per summit; paired valleys share their
        // endpoints, so snow forks around exposed rock instead of filling gullies.
        var spurs=new System.Collections.Generic.List<Branch>();
        var valleys=new System.Collections.Generic.List<Branch>();
        foreach(int summit in summits)
        {
            int count=2+(Hash(seed+summit*317)>.45f?1:0);
            int first=spurs.Count;
            for(int i=0;i<count;i++)
            {
                float offset=(i-(count-1)*.5f)*Mathf.Lerp(28,42,Hash(seed+summit*331))/pixelScale.x;
                int bx=Mathf.Clamp(Mathf.RoundToInt(summit+offset),0,W-1);
                float sign=i<(count*.5f)?-1:1;
                float angle=Mathf.Lerp(25,55,Hash(seed+summit*347+i*43))*Mathf.Deg2Rad;
                spurs.Add(new Branch {x=bx,top=ridge[bx]-4,
                    length=Mathf.Lerp(180,330,Hash(seed+summit*359+i*71)),
                    bendA=sign*Mathf.Lerp(6,18,Hash(seed+summit+i*179)),
                    bendB=-sign*Mathf.Lerp(6,18,Hash(seed+summit+i*181)),
                    bendC=sign*Mathf.Lerp(6,18,Hash(seed+summit+i*191)),
                    knotA=.25f+Mathf.Lerp(-8,8,Hash(seed+summit+i*193))/180f,
                    knotB=.5f+Mathf.Lerp(-8,8,Hash(seed+summit+i*197))/180f,
                    knotC=.75f+Mathf.Lerp(-8,8,Hash(seed+summit+i*199))/180f,
                    lean=sign/Mathf.Tan(angle), width=Mathf.Lerp(8,18,Hash(seed+summit*373+i*89))});
            }
            for(int i=first;i<spurs.Count-1;i++)
            {
                var a=spurs[i]; var b=spurs[i+1];
                int bx=Mathf.Clamp(Mathf.RoundToInt((a.x+b.x)*.5f),0,W-1);
                valleys.Add(new Branch {x=bx,top=ridge[bx]-4,
                    bendA=(a.bendA+b.bendA)*.5f,bendB=(a.bendB+b.bendB)*.5f,bendC=(a.bendC+b.bendC)*.5f,
                    knotA=(a.knotA+b.knotA)*.5f,knotB=(a.knotB+b.knotB)*.5f,knotC=(a.knotC+b.knotC)*.5f,
                    lean=(a.lean+b.lean)*.5f,length=(a.length+b.length)*.5f,
                    width=Mathf.Lerp(10,22,Hash(seed+summit*409+i*127))});
            }
        }
        var spurDrawn=new bool[spurs.Count];
        float spurStrength=layer==0?.08f:layer==1?.04f:0;
        float valleyStrength=layer==0?.08f:layer==1?.04f:.03f;
        // Tree tips stay within the lowest 7% of the visible mountain.
        var trees=new int[W]; var treeFeet=new int[W];
        if(layer<2)
            for(int x=4,index=0;x<W-4;index++)
            {
                int height=6+Mathf.RoundToInt(6*Hash(seed+index*239));
                int bottom=Mathf.FloorToInt(visibleFoot+Mathf.Max(0,(crest-visibleFoot)*.07f-height)*Hash(seed+index*251));
                for(int dx=-4;dx<=4;dx++)
                    { trees[x+dx]=bottom+height-Math.Abs(dx)*2; treeFeet[x+dx]=bottom; }
                x+=8+Mathf.RoundToInt(8*Hash(seed+index*269));
            }
        double visibleLuma=0; int visibleArea=0;
        for(int x=0;x<W;x++)
            for(int y=0;y<=Mathf.CeilToInt(ridge[x]+.5f);y++)
            {
                float depth=ridge[x]-y;
                float down=(ridge[x]-y)/Mathf.Max(1,ridge[x]-visibleFoot);
                // Three dark-to-light rock beds; eroded boundaries break straight color bands.
                float strata=down+Noise2(x/38f,y/46f,seed+42)*.19f+Noise2(x/12f,y/22f,seed+211)*.08f;
                int level=Mathf.Clamp(Mathf.FloorToInt(strata*RockLevels),0,RockLevels-1);
                Color basis=rock[level];
                // Feather only the eroded joins; retain three readable rock-color levels.
                for(int bed=1;bed<RockLevels;bed++)
                    if(Mathf.Abs(strata-bed/(float)RockLevels)<.055f)
                        basis=Color.Lerp(rock[bed-1],rock[bed],Mathf.SmoothStep(0,1,
                            Mathf.InverseLerp(bed/(float)RockLevels-.055f,bed/(float)RockLevels+.055f,strata)));
                Color c=layer==3?Color.Lerp(Hex("#91A2B3"),air,mix):Color.Lerp(Rock(basis,false,mix,air),Rock(basis,true,mix,air),faceLight[x]);
                if(layer<3)
                {
                    // Broad irregular relief plus sparse, narrow vertical rock fractures.
                    float grain=Noise2((x+(crest-y)*.12f)/14f,y/42f,seed+137)*.055f
                        +Noise2(x/5f,y/14f,seed+433)*.025f;
                    c.r+=grain*(1-mix); c.g+=grain*(1-mix); c.b+=grain*(1-mix);
                    for(int i=0;i<streaks.Length;i++)
                    {
                        var stroke=streaks[i];
                        if(stroke.width<4 || stroke.length<24 || stroke.width*pixelScale.x<4 || stroke.length*pixelScale.y<24) continue;
                        float dy=stroke.top-y;
                        int left=Mathf.RoundToInt(stroke.Center(y))-stroke.width/2;
                        if(dy<0 || dy>=stroke.length || x<left || x>=left+stroke.width) continue;
                        c.r*=stroke.contrast; c.g*=stroke.contrast; c.b*=stroke.contrast;
                        streakDrawn[i]=true;
                    }
                    int ridgeWidth=1+(int)(Hash(seed+apex[x])*1.999f);
                    if(x>=apex[x] && x<apex[x]+ridgeWidth && down<.45f)
                        c=Color.Lerp(c,Color.Lerp(transition,air,mix),.18f*(1-down/.45f)*Mathf.Clamp01(.6f+Noise(y/14f,seed+apex[x])*.5f));
                }
                // Save the unsnowed bed so the same geometry cuts rock channels
                // through both the summit cap and the old decorative snow streaks.
                if(layer<2)
                {
                    float facet=Facet(x*pixelScale.x,y*pixelScale.y,seed+1601)*(1-mix);
                    c.r+=facet; c.g+=facet; c.b+=facet;
                }
                Color bareRock=c;
                Color snowColor=Color.Lerp(snow,air,mix);
                const int edgeWidth=1;
                if(y>=fadeTop && depth<caps[x])
                    c=Color.Lerp(c,snowColor,Mathf.Clamp01(.55f+Noise2(x/8f,y/10f,seed+301)*.30f));
                else if(y>=fadeTop && depth<caps[x]+edgeWidth)
                    c=Color.Lerp(c,Hex("#D5E2EE"),.5f); // Alpha-composite the 1 px snow edge over solid rock.
                float spurMask=0, valleyMask=0;
                for(int i=0;i<spurs.Count;i++)
                {
                    var b=spurs[i]; float along;
                    float mask=b.Mask(x,y,pixelScale,out along);
                    if(mask>0) spurDrawn[i]=true;
                    spurMask=Mathf.Max(spurMask,mask);
                }
                foreach(var b in valleys)
                {
                    float along;
                    valleyMask=Mathf.Max(valleyMask,b.Mask(x,y,pixelScale,out along));
                }
                float relief=spurStrength*spurMask-valleyStrength*valleyMask;
                // Deep channels stay snow-free; soft sides avoid new full-height seams.
                c=Color.Lerp(c,bareRock,Mathf.SmoothStep(0,1,valleyMask));
                Color beforeRelief=c;
                c.r+=relief; c.g+=relief; c.b+=relief;
                float delta=Luma((Color32)c)-Luma((Color32)beforeRelief);
                SpurContrast[layer]=Mathf.Max(SpurContrast[layer],delta);
                GullyContrast[layer]=Mathf.Max(GullyContrast[layer],-delta);
                float footHaze=.35f*(1-Mathf.Clamp01((y-visibleFoot)/Mathf.Max(1,(crest-visibleFoot)*.10f)));
                float fog=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((y-visibleFoot)/hazeHeight));
                c=Color.Lerp(c,haze,1-(1-fog)*(1-footHaze));
                if(layer<2 && y>=treeFeet[x] && y<trees[x])
                {
                    Color tree=Color.Lerp(Hex("#26313A"),air,mix);
                    c=Color.Lerp(c,tree,(layer==0?.45f:.2f)*(1-fog));
                }
                // A 2x2 subpixel box resolve preserves half-texel ridge coverage.
                float coverage=0;
                for(int sx=0;sx<2;sx++)
                {
                    float sampleX=Mathf.Clamp(x+(sx==0?-.25f:.25f),0,W-1);
                    int left=Mathf.FloorToInt(sampleX);
                    float sampleRidge=Mathf.Lerp(ridge[left],ridge[Mathf.Min(W-1,left+1)],sampleX-left);
                    for(int sy=0;sy<2;sy++)
                        if(y+(sy+.5f)*.5f<=sampleRidge+.5f) coverage+=.25f;
                }
                float footAlpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(visibleFoot,fadeTop,y));
                c.a=coverage*footAlpha*(layer==0?.85f:1f)
                    *Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(x,W-1-x)/(W*Feather)));
                Color32 pixel=c; pixels[y*W+x]=pixel;
                if(y>=visibleFoot) { visibleLuma+=Luma(pixel); visibleArea++; }
            }
        // The overscanned card stretches one original texel over ~5 screen
        // pixels. Resolve ONLY snow at 4x horizontal precision, replicating the
        // existing rock texels exactly so 3px channels do not become dotted lines.
        int snowResolve=layer<3?4:1, outputWidth=W*snowResolve;
        var resolved=new Color32[outputWidth*H];
        for(int y=0;y<H;y++)
            for(int x=0;x<outputWidth;x++)
            {
                if(layer!=2) resolved[y*outputWidth+x]=pixels[y*W+x/snowResolve];
                else
                {
                    // Preserve L3's existing bilinear rock sampling as well.
                    float sample=(x+.5f)/snowResolve-.5f;
                    int left=Mathf.FloorToInt(sample);
                    resolved[y*outputWidth+x]=Color.Lerp(pixels[y*W+Mathf.Clamp(left,0,W-1)],
                        pixels[y*W+Mathf.Clamp(left+1,0,W-1)],sample-left);
                }
            }
        for(int i=0;i<gullies.Count;i++)
        {
            var g=gullies[i];
            int x0=Mathf.Max(0,Mathf.FloorToInt((g.x-g.width*.5f)/pixelScale.x*snowResolve));
            int x1=Mathf.Min(outputWidth-1,Mathf.CeilToInt((g.x+g.bend+g.width*.5f)/pixelScale.x*snowResolve));
            for(int y=Mathf.Max(0,Mathf.CeilToInt(g.top-g.length));y<=Mathf.FloorToInt(g.top);y++)
            {
                if(y<fadeTop) continue;
                float t=(g.top-y)/g.length;
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,t));
                float footHaze=.35f*(1-Mathf.Clamp01((y-visibleFoot)/Mathf.Max(1,(crest-visibleFoot)*.10f)));
                float fog=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((y-visibleFoot)/hazeHeight));
                Color target=Color.Lerp(gullySnow,haze,1-(1-fog)*(1-footHaze));
                for(int x=x0;x<=x1;x++)
                {
                    float sample=(x+.5f)/snowResolve-.5f;
                    int ix=Mathf.Clamp(Mathf.RoundToInt(sample),0,W-1);
                    float height=Mathf.Lerp(ridge[Mathf.Clamp(Mathf.FloorToInt(sample),0,W-1)],
                        ridge[Mathf.Clamp(Mathf.CeilToInt(sample),0,W-1)],sample-Mathf.Floor(sample));
                    float down=(height-y)/Mathf.Max(1,height-visibleFoot);
                    if(down<0 || down>=.35f || apex[ix]!=g.summit || faceLight[ix]>.05f) continue;
                    float distance=Mathf.Abs(sample*pixelScale.x-g.Center(t));
                    float edge=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(g.width*.5f-1,g.width*.5f,distance));
                    float mask=edge*fade*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.35f,down)));
                    if(mask<=0) continue;
                    int index=y*outputWidth+x;
                    Color c=resolved[index]; float alpha=c.a;
                    c=Color.Lerp(c,target,mask*.78f); c.a=alpha;
                    resolved[index]=c;
                    maxSnowDepth=Mathf.Max(maxSnowDepth,down*100); gullyDrawn[i]=true;
                }
            }
        }
        visibleLuma=0; visibleArea=0;
        for(int y=Mathf.CeilToInt(visibleFoot);y<H;y++)
            for(int x=0;x<outputWidth;x++)
                if(y<=Mathf.CeilToInt(ridge[x/snowResolve]+.5f))
                    { visibleLuma+=Luma(resolved[y*outputWidth+x]); visibleArea++; }
        SpurCounts[layer]=Array.FindAll(spurDrawn,v=>v).Length;
        if(SpurCounts[layer]<8) throw new InvalidOperationException("MTN12 visible branch count gate");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN12_LAYER] L{0} spurs={1} gullies={2} spur_contrast={3:F6} gully_contrast={4:F6}",
            layer+1,SpurCounts[layer],valleys.Count,SpurContrast[layer],GullyContrast[layer]));
        LayerLuma[layer]=(float)(visibleLuma/Math.Max(1,visibleArea));
        if(layer<3)
        {
            RockStreaks[layer]=Array.FindAll(streakDrawn,v=>v).Length;
            SnowGullies[layer]=Array.FindAll(gullyDrawn,v=>v).Length;
            // Same-bed samples include the actual layer's atmospheric mix.
            float darkest=1,brightest=0;
            foreach(var basis in rock)
            {
                Color light=Rock(basis,true,0,air), shade=Rock(basis,false,0,air);
                minShadowRatio=Mathf.Min(minShadowRatio,Luma(shade)/Luma(light));
                minShadeLuma=Mathf.Min(minShadeLuma,Luma(shade));
                darkest=Mathf.Min(darkest,Luma(shade)); brightest=Mathf.Max(brightest,Luma(light));
                minFaceDelta=Mathf.Min(minFaceDelta,Luma(Rock(basis,true,mix,air))-Luma(Rock(basis,false,mix,air)));
            }
            maxFaceGap=Mathf.Max(maxFaceGap,brightest-darkest+.04f); // Include both facet transition extremes.
            if(RockStreaks[layer]<8 || RockStreaks[layer]>14 || SnowGullies[layer]>summits.Count*2)
                throw new InvalidOperationException("MTN10 painted streak/gully count gate");
        }
        int footRow=Mathf.CeilToInt(visibleFoot);
        float jump=0;
        for(int x=1;x<W;x++)
        {
            jump=Mathf.Max(jump,Mathf.Abs(Luma(pixels[footRow*W+x])-Luma(pixels[footRow*W+x-1])));
            jump=Mathf.Max(jump,Mathf.Abs(Luma(pixels[(footRow+1)*W+x])-Luma(pixels[footRow*W+x])));
        }
        maxFootJump=Mathf.Max(maxFootJump,jump);
        if(jump>.04f) throw new InvalidOperationException("MTN10 foot continuity gate");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN10_PAINT] L{0} rock_streaks={1} snow_gullies={2} luma={3:F4} foot_jump={4:F6}",
            layer+1,streaks.Length,gullies.Count,LayerLuma[layer],jump));
        string path=$"Assets/Art/Textures/MtnLayer_{layer+1}.png";
        var tex=new Texture2D(outputWidth,H,TextureFormat.RGBA32,false); tex.SetPixels32(resolved); tex.Apply();
        File.WriteAllBytes(path,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Default; importer.filterMode=layer<2?FilterMode.Point:FilterMode.Bilinear;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false; importer.mipmapFilter=TextureImporterMipFilter.BoxFilter;
        importer.mipMapsPreserveCoverage=false; importer.borderMipmap=false; importer.anisoLevel=0; importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=false; importer.wrapMode=TextureWrapMode.Clamp;
        importer.maxTextureSize=outputWidth; importer.npotScale=TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    // Run AFTER capture_runtime.sh: measure the encoded screenshot, not texture estimates.
    public static void MeasureScreenshot()
    {
        string path="Shots/art_runtime.png";
        var frame=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        if(!frame.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("MTN11 screenshot decode");
        int w=frame.width,h=frame.height;
        var pixels=frame.GetPixels32();
        int x0=(int)(w*.05),x1=(int)(w*.95),y0=(int)(h*.10),y1=(int)(h*.45);
        var columns=new double[x1-x0];
        double sum=0;
        for(int x=x0;x<x1;x++)
        {
            for(int y=y0;y<y1;y++) columns[x-x0]+=Luma(pixels[(h-1-y)*w+x]);
            columns[x-x0]/=y1-y0; sum+=columns[x-x0];
        }
        var differences=new double[columns.Length-1];
        for(int x=0;x<differences.Length;x++) differences[x]=Math.Abs(columns[x+1]-columns[x]);
        Array.Sort(differences);
        double index=(differences.Length-1)*.99;
        int lower=(int)Math.Floor(index),upper=(int)Math.Ceiling(index);
        double seam=differences[lower]+(differences[upper]-differences[lower])*(index-lower);
        double luma=sum/columns.Length;
        float delta=1;
        foreach(float mix in new[]{0f,.38f,.68f})
            foreach(string tint in new[]{"#5A6878","#6B7888","#7C8896"})
                delta=Mathf.Min(delta,Luma(Rock(Hex(tint),true,mix,Hex("#DAE5EF")))-Luma(Rock(Hex(tint),false,mix,Hex("#DAE5EF"))));
        // Screenshot-only skyline locator. First sustained sky-to-snow rise,
        // averaged over 5 columns to reject isolated snowflakes. Measure horizontal
        // 3px differences around that silhouette, not the snow/rock face contrast.
        int stair=0, magenta=0;
        for(int i=0;i<pixels.Length;i++)
            if(pixels[i].r>150 && pixels[i].b>150 && pixels[i].g<100) magenta++;
        var samples=new System.Text.StringBuilder("x,y,delta_luma_3px\n");
        for(int i=0;i<200;i++)
        {
            int x=Mathf.RoundToInt(w*(.05f+.90f*i/199f));
            int edge=-1;
            for(int y=(int)(h*.14f);y<(int)(h*.42f);y++)
            {
                float rise=0;
                for(int dx=-2;dx<=2;dx++)
                    rise+=Luma(pixels[(h-2-y)*w+x+dx])-Luma(pixels[(h-1-y)*w+x+dx]);
                if(rise/5>.025f) { edge=y; break; }
            }
            if(edge<0) throw new InvalidOperationException("MTN13 missing screenshot ridge at x="+x);
            float jump=0;
            for(int dy=-1;dy<=1;dy++)
            {
                int row=(h-1-edge-dy)*w;
                jump=Mathf.Max(jump,Mathf.Abs(Luma(pixels[row+x+3])-Luma(pixels[row+x])));
            }
            samples.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2:F6}\n",x,edge,jump);
            if(jump>.25f) stair++;
        }
        File.WriteAllText("Shots/mtn13_ridge_samples.csv",samples.ToString());
        double ridgeStair=stair/200.0;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN13_MEASURE] ridge_stair_px={0:F6} exceed={1}/200 magenta_px={2} sample_x=0.05..0.95 ridge_search_y=0.14..0.42 locator=first_5col_rise_gt_0.025 radius=1 axis=x delta_px=3 threshold=0.25",ridgeStair,stair,magenta));
        if(ridgeStair>.05 || magenta!=0) throw new InvalidOperationException("MTN13 screenshot gate");
        // Horizontal adjacent-pixel luma differences at screen y=0.63. The
        // side bands exclude church/tower geometry and measure the mountain foot.
        var footGrad=new System.Collections.Generic.List<float>();
        int footY=Mathf.RoundToInt(h*.63f);
        for(int x=(int)(w*.02f);x<(int)(w*.98f)-1;x++)
        {
            if(x>=w*.30f && x<w*.76f) continue;
            int row=(h-1-footY)*w;
            footGrad.Add(Mathf.Abs(Luma(pixels[row+x+1])-Luma(pixels[row+x])));
        }
        footGrad.Sort();
        float footEdge=footGrad[Mathf.FloorToInt((footGrad.Count-1)*.95f)];
        // Detect persistent dark 1-2 px strokes against their immediate sides.
        // Runs must span >=24 output rows; individual snow particles are rejected.
        int stray=0;
        for(int x=(int)(w*.02f);x<(int)(w*.98f)-3;x++)
        {
            if(x>=w*.30f && x<w*.76f) continue;
            for(int width=1;width<=2;width++)
            {
                int run=0;
                for(int y=(int)(h*.35f);y<(int)(h*.65f);y++)
                {
                    int row=(h-1-y)*w;
                    float sides=Mathf.Min(Luma(pixels[row+x-1]),Luma(pixels[row+x+width]));
                    float center=0;
                    for(int dx=0;dx<width;dx++) center=Mathf.Max(center,Luma(pixels[row+x+dx]));
                    run=sides-center>.035f?run+1:0;
                    if(run==24) stray++;
                }
            }
        }
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN15] foot_fade_pct=18 min_streak_px=4 max_streak_contrast=1.0 stray_strokes={0} foot_edge_grad={1:F6}",stray,footEdge));
        UnityEngine.Object.DestroyImmediate(frame);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN11] rock_tints=#5A6878/#6B7888/#7C8896 lit_shadow_delta={0:F6} treeline_pct=0.07 treeline_alpha=0.45 overscan=0.12 feather=0.08 seam_max_grad={1:F6} height_scale={2:F6}",delta,seam,HeightScale));
        Debug.Log(string.Format(CultureInfo.InvariantCulture,"[MTN11] mountain_luma={0:F6}",luma));
        if(stray!=0 || footEdge>.02f) throw new InvalidOperationException("MTN15 screenshot gate");
        if(luma<.42 || luma>.60 || seam>.035 || delta<.05f || delta>.26f)
            throw new InvalidOperationException("MTN11 screenshot gate");
    }
    // Repaint the existing scene in isolation; do not regenerate church, interior or cameras.
    public static void Repaint()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/ArtSpike.unity");
        var oldRidges=Array.Find(scene.GetRootGameObjects(),go=>go.name=="Mountain Ridges");
        var painted=GameObject.Find("Painted Mountain Layers");
        if(painted!=null) UnityEngine.Object.DestroyImmediate(painted);
        Build(Camera.main,oldRidges);
        EditorSceneManager.SaveScene(scene);
    }
    public static void Build(Camera cam,GameObject oldRidges)
    {
        if(oldRidges==null) throw new InvalidOperationException("MTN4: missing rollback ridges");
        oldRidges.SetActive(false);
        var root=new GameObject("Painted Mountain Layers").transform;
        float aspect=1920f/1080f;
        float[] distances={900,1300,1800,2200},tops={.32f,.28f,.24f,.20f};
        var measured=new float[4];
        var widths=new float[4]; var fills=new float[4];
        var poses=new System.Collections.Generic.List<ShotPose>();
        foreach(var pose in UnityEngine.Object.FindFirstObjectByType<ShotRig>().poses)
            if(pose.name!="art_ground") poses.Add(pose); // ground inspection looks away from the church/backdrop.
        var controller=UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        float runtimeFov=PlayerController.FollowFieldOfView(controller.GetComponent<MistReveal>().church,controller.ground);
        foreach(float z in new[]{-88f,-89f,-59f,-29f,-15f,-9f})
        {
            // Use the same geometry-derived FOV as the actual runtime camera.
            var player=new Vector3(0,Mathf.Tan(6*Mathf.Deg2Rad)*z,z);
            poses.Add(ShotRig.Look("follow",player+PlayerController.FollowOffset,
                player+Vector3.up*PlayerController.LookHeight,runtimeFov));
        }
        Array.Clear(SpurContrast,0,4); Array.Clear(GullyContrast,0,4);
        maxSnowDepth=0; minSnowWidth=float.PositiveInfinity; maxSnowWidth=0; maxSnowReversals=0;
        ridgeBreaks=0; straightSegments=0; maxFacePixels=0;
        minShadowRatio=1; minShadeLuma=1; maxFaceGap=0;
        maxPeakStdPct=0; maxFootJump=0; minCap=int.MaxValue; maxCap=0; minFaceDelta=1;
        for(int layer=0;layer<4;layer++)
        {
            float d=distances[layer];
            float viewH=2*d*Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad);
            float width=viewH*aspect*1.04f,height=width*.5f;
            // Measure maximum alpha only inside the visible viewport (exclude overscan).
            float peak=0; int peakX=0;

            float bottom=1-tops[layer]-(OriginalCrests[layer]+1f)/H*height/viewH;
            height*=HeightScale;
            // Current locked shot: the distant ground meets the mountain at 74% screen Y.

            Vector3 center=cam.transform.position+cam.transform.forward*d+
                cam.transform.up*((bottom-.5f)*viewH+height*.5f)+
                cam.transform.right*((layer-1.5f)*150f/1920f*viewH*aspect);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name=$"MtnLayer_{layer+1}"; quad.transform.SetParent(root,false);
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetPositionAndRotation(center,cam.transform.rotation);
            // Intersect the widest horizontal camera frustum with this card plane. Include
            // asymmetric shot positions: a total width alone can hide a side gap.
            float extent=width/2;
            foreach(var pose in poses)
            {
                var rotation=Quaternion.Euler(pose.euler);
                float tan=Mathf.Tan(pose.fov*.5f*Mathf.Deg2Rad);
                for(int x=-1;x<=1;x+=2)
                {
                    var ray=rotation*new Vector3(x*1.30f*tan*aspect,0,1);
                    float denominator=Vector3.Dot(ray,quad.transform.forward);
                    if(denominator<=.01f) continue;
                    float t=Vector3.Dot(center-pose.position,quad.transform.forward)/denominator;
                    var local=Quaternion.Inverse(quad.transform.rotation)*(pose.position+ray*t-center);
                    extent=Mathf.Max(extent,Mathf.Abs(local.x)*1.02f);
                }
            }
            width=extent*2*(1+Overscan);
            quad.transform.localScale=new Vector3(width,height,1);
            var pixelScale=new Vector2(width/(viewH*aspect)*1920/W,height/viewH*1080/H);
            float[] ridge=Profile(layer,pixelScale,out int peaks,out float std);
            PeakCounts[layer]=peaks;
            for(int x=20;x<W-20;x++) if(ridge[x]>peak) {peak=ridge[x];peakX=x;}
            float visibleFoot=Mathf.Clamp(((1-bottom)-.74f)*viewH/height*H,0,peak-1);
            var texture=Paint(layer,ridge,visibleFoot,pixelScale);
            widths[layer]=width; fills[layer]=float.PositiveInfinity;
            foreach(var pose in poses)
            {
                var rotation=Quaternion.Euler(pose.euler);
                float tan=Mathf.Tan(pose.fov*.5f*Mathf.Deg2Rad);
                float low=0,high=3;
                for(int iteration=0;iteration<20;iteration++)
                {
                    float f=(low+high)/2; bool covered=true;
                    for(int x=-1;x<=1;x+=2)
                    {
                        var ray=rotation*new Vector3(x*f*tan*aspect,0,1);
                        float denominator=Vector3.Dot(ray,quad.transform.forward);
                        float t=Vector3.Dot(center-pose.position,quad.transform.forward)/denominator;
                        var local=Quaternion.Inverse(quad.transform.rotation)*(pose.position+ray*t-center);
                        covered &= denominator>.001f && t>0 && Mathf.Abs(local.x)<=width/2;
                    }
                    if(covered) low=f; else high=f;
                }
                fills[layer]=Mathf.Min(fills[layer],low);
            }

            // L3 physically remains at 1800m. Bounds overlap the frustum because its
            // background shader clamps far depth; no camera clipping setting is changed.
            var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);
            mesh.name=quad.name; mesh.bounds=new Bounds(Vector3.zero,new Vector3(1,1,4000));
            string meshPath=$"Assets/Settings/MtnLayer_{layer+1}.asset";
            AssetDatabase.DeleteAsset(meshPath); AssetDatabase.CreateAsset(mesh,meshPath);
            quad.GetComponent<MeshFilter>().sharedMesh=mesh;
            var mat=new Material(Shader.Find("Mistbell/PaintedMountain")); mat.SetTexture("_MainTex",texture);
            // 大气透视：L1 0.06 → L4 0.72，越远越融进天空；底部再叠 0.25 地雾。
            float[] haze={.06f,.26f,.48f,.72f};
            mat.SetColor("_HazeColor",Hex("#C9D7E4"));
            mat.SetFloat("_Haze",haze[Mathf.Clamp(layer,0,3)]);
            mat.SetFloat("_HazeBottom",.25f);
            // L3/L4 share the far-depth clamp: explicitly paint farther cards first.
            mat.renderQueue=2450-layer;
            string matPath=$"Assets/Settings/M_MtnLayer_{layer+1}.mat";
            AssetDatabase.DeleteAsset(matPath); AssetDatabase.CreateAsset(mat,matPath);
            var renderer=quad.GetComponent<MeshRenderer>(); renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            Vector3 crest=quad.transform.TransformPoint(new Vector3((peakX+.5f)/W-.5f,(peak+.5f)/H-.5f,0));
            // Use explicit output aspect without modifying any camera property.
            measured[layer]=1-cam.WorldToViewportPoint(crest).y;
            Vector3 foot=quad.transform.TransformPoint(new Vector3(0,-.5f,0));
            if(fills[layer]<1.25f || measured[layer]>.30f || bottom>0 || foot.y>-20)
                throw new InvalidOperationException($"MTN8 silhouette/foot gate L{layer+1} fill={fills[layer]} top={measured[layer]} bottom={bottom} foot={foot.y}");
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[MTN4_LAYER] L{0} peaks={1} ridge_std_px={2:F2} distance_m={3:F0} width_m={4:F2} top_viewport_y={5:F4} foot_viewport_y={6:F4} foot_world_y={7:F2}",
                layer+1,peaks,std,d,width,measured[layer],1-bottom,foot.y));
        }
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[GULLY] count_l1={0} count_l2={1} max_len_pct={2:F4} max_reversals={3} width_px={4:F0}-{5:F0} side=shadow_only",
            SnowGullies[0],SnowGullies[1],maxSnowDepth,maxSnowReversals,minSnowWidth,maxSnowWidth));
        if(maxSnowDepth>35 || maxSnowReversals>1 || minSnowWidth<3 || maxSnowWidth>7
            || SnowGullies[0]==0 || SnowGullies[1]==0)
            throw new InvalidOperationException("GULLY short shadow snow gate");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN14] shadow_ratio={0:F6} shade_luma={1:F6} face_luma_gap={2:F6} ridge_breaks={3} max_face_px={4} straight_segments={5}",
            minShadowRatio,minShadeLuma,maxFaceGap,ridgeBreaks,maxFacePixels,straightSegments));
        if(minShadowRatio<.66f || maxFaceGap>.26f || ridgeBreaks<6*42 || maxFacePixels>200 || straightSegments!=0)
            throw new InvalidOperationException("MTN14 face/ridge gate");
        Debug.Log("[MTN13] rock_grid=2048x1024 snow_resolve=L1..L3:8192x1024/L4:2048x1024 filter=L1:Point/L2:Point/L3:Bilinear/L4:Bilinear snow_edge_alpha=0.5 supersample=2x foot_haze_pct=10");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN8] layer_width_m={0:F2}/{1:F2}/{2:F2} screen_fill_pct={3:F4}/{4:F4}/{5:F4} top_y={6:F4}/{7:F4}/{8:F4}",
            widths[0],widths[1],widths[2],fills[0],fills[1],fills[2],measured[0],measured[1],measured[2]));
        if(SpurContrast[0]<.05f || GullyContrast[0]<.06f)
            throw new InvalidOperationException("MTN12 raster contrast gate");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN12] spurs={0}/{1}/{2} spur_angle=25..55 spur_width_px=8..18 gully_width_px=10..22 spur_contrast={3:F6} gully_contrast={4:F6}",
            SpurCounts[0],SpurCounts[1],SpurCounts[2],SpurContrast[0],GullyContrast[0]));
        float gradient=LayerLuma[2]-LayerLuma[0];
        if(gradient<.18f || LayerLuma[1]<=LayerLuma[0] || LayerLuma[1]>=LayerLuma[2])
            throw new InvalidOperationException("MTN10 layer luminance gate");
        if(RockLevels!=3 || minFaceDelta<.05f || minFaceDelta>.26f || minCap!=5 || maxCap!=24 || root.childCount!=4)
            throw new InvalidOperationException($"MTN10 drawing gate delta={minFaceDelta} caps={minCap}..{maxCap}");
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[MTN10] layers={0} rock_levels={1} rock_streaks={2}/{3} lit_shadow_delta={4:F6} snowcap_px={5}..{6} snow_gullies={7}/{8} treeline=0.45/0.2/no silhouette_peaks={9}/{10}/{11}",
            root.childCount,RockLevels,RockStreaks[0],RockStreaks[1],minFaceDelta,minCap,maxCap,
            SnowGullies[0],SnowGullies[1],PeakCounts[0],PeakCounts[1],PeakCounts[2]));
    }
}
