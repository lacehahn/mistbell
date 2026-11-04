using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Dedicated inspection rig, created only by the task flag. Gameplay rig is untouched.
public sealed class PathTextureCapture:MonoBehaviour {
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Init(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-demo-path-texture")>=0)new GameObject("Path Texture Inspection").AddComponent<PathTextureCapture>();}
 IEnumerator Start(){
  yield return new WaitForSecondsRealtime(2);
  var player=FindFirstObjectByType<PlayerController>();var camera=Camera.main;
  var target=new Vector3(ClimbRoute.CenterX(-110),0,-110);
  player.ground.Raycast(new Ray(target+Vector3.up*100,Vector3.down),out var hit,200);target.y=hit.point.y+.08f;
  // Disable controller only in this inspection run to keep its follow update from moving the camera.
  player.enabled=false;
  var offset=new Vector3(1,2,-2).normalized*3f;
  camera.transform.position=target+offset;camera.transform.LookAt(target);camera.fieldOfView=55;
  var additional=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();additional.renderPostProcessing=false;
  yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();
  var root=new DirectoryInfo(Application.dataPath);while(root!=null && root.Name!="Builds")root=root.Parent;
  string dir=Path.Combine(root!=null?root.Parent.FullName:Directory.GetCurrentDirectory(),"Shots");Directory.CreateDirectory(dir);
  ScreenCapture.CaptureScreenshot(Path.Combine(dir,"path_closeup.png"));
  Debug.Log(FormattableString.Invariant($"[PATH_CLOSEUP] distance_m={Vector3.Distance(camera.transform.position,target):F3} target={target.ToString("F3")} size={Screen.width}x{Screen.height} post=false"));
  yield return new WaitForSecondsRealtime(1);
  // Additional same-lighting samples allow comparison of the two authored altitude endpoints.
  foreach(var z in new[]{-175f,-30f}){
   target=new Vector3(ClimbRoute.CenterX(z),0,z);player.ground.Raycast(new Ray(target+Vector3.up*100,Vector3.down),out hit,200);target.y=hit.point.y+.08f;
   camera.transform.position=target+offset;camera.transform.LookAt(target);
   yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();
   ScreenCapture.CaptureScreenshot(Path.Combine(dir,z< -100?"path_lower.png":"path_upper.png"));yield return new WaitForSecondsRealtime(.5f);
  }
  target=new Vector3(0,0,-11.7f);player.ground.Raycast(new Ray(target+Vector3.up*100,Vector3.down),out hit,200);target.y=hit.point.y+.8f;
  camera.transform.position=target+new Vector3(1,2,-3);camera.transform.LookAt(target);
  yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"path_steps.png"));
  yield return new WaitForSecondsRealtime(1);Application.Quit(0);
 }
}
