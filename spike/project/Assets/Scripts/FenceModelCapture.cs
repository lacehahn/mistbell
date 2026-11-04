using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Inspection camera only, activated by a dedicated acceptance flag.
public sealed class FenceModelCapture:MonoBehaviour {
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Init(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-demo-fence-model")>=0)new GameObject("Fence Model Inspection").AddComponent<FenceModelCapture>();}
 IEnumerator Start(){
  yield return new WaitForSecondsRealtime(2);var player=FindFirstObjectByType<PlayerController>();player.enabled=false;var camera=Camera.main;
  var root=GameObject.Find("FM4_Root");Transform chosen=null;float best=999;
  foreach(Transform segment in root.transform){var f=segment.GetComponent<MeshFilter>();if(f==null||f.sharedMesh.name!="Fence_C")continue;float score=Mathf.Abs(segment.position.z+110);if(score<best){chosen=segment;best=score;}}
  if(chosen==null){Debug.LogError("Fence_C missing");Application.Quit(1);yield break;}
  Vector3 target=chosen.position+new Vector3(0,.6f,1.05f);Vector3 road=new Vector3(ClimbRoute.CenterX(target.z),target.y,target.z);Vector3 toward=(road-target).normalized;var sun=RenderSettings.sun;if(sun!=null&&Vector3.Dot(toward,-sun.transform.forward)<0)toward=-toward;
  camera.transform.position=target+toward*2.55f+Vector3.up*.7f;camera.transform.LookAt(target);camera.fieldOfView=57;camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
  yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();
  var dir=new DirectoryInfo(Application.dataPath);while(dir!=null&&dir.Name!="Builds")dir=dir.Parent;string output=Path.Combine(dir!=null?dir.Parent.FullName:Directory.GetCurrentDirectory(),"Shots");Directory.CreateDirectory(output);
  var frame=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"fence_closeup.png"),frame.EncodeToPNG());Destroy(frame);
  var batch=GameObject.Find("FM4_Batch");var collider=batch.GetComponent<MeshCollider>();var mesh=batch.GetComponent<MeshFilter>().sharedMesh;var indices=mesh.triangles;var colors=mesh.colors;var texture=(Texture2D)batch.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap");var csv=new StringBuilder("x,y,level\n");int samples=0;
  for(int y=8;y<Screen.height-8;y+=4)for(int x=8;x<Screen.width-8;x+=4){var ray=camera.ScreenPointToRay(new Vector3(x+.5f,y+.5f));if(!collider.Raycast(ray,out var hit,4)||hit.distance>3.8f||Vector3.Dot(hit.normal,sun!=null?-sun.transform.forward:Vector3.up)<.25f)continue;int index=hit.triangleIndex*3;if(colors[indices[index]].a<.99f)continue;var uv=hit.textureCoord;Color32 pixel=texture.GetPixel(Mathf.FloorToInt(Mathf.Repeat(uv.x,1)*texture.width),Mathf.FloorToInt(Mathf.Repeat(uv.y,1)*texture.height));if(pixel.g>90)continue;int level=pixel.r<62?0:pixel.r<91?1:2;csv.Append(x).Append(',').Append(Screen.height-1-y).Append(',').Append(level).Append('\n');samples++;}
  File.WriteAllText(Path.Combine(output,"fence_closeup_pixels.csv"),csv.ToString());Debug.Log("[FENCE_PIXELS] sunlit_wood_samples="+samples);
  Debug.Log(FormattableString.Invariant($"[FENCE_CLOSEUP] distance_m={Vector3.Distance(camera.transform.position,target):F6} variant=C size={Screen.width}x{Screen.height} post=false"));yield return new WaitForSecondsRealtime(.5f);Application.Quit(0);
 }
}
