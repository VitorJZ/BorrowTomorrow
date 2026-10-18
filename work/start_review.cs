using UnityEditor;
using UnityEngine;
var gt=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var gw=EditorWindow.GetWindow(gt);var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
gt.GetMethod("SetCustomResolution",f).Invoke(gw,new object[]{new Vector2(1920,1080),"Art sample"});gw.Focus();
EditorApplication.isPlaying=true;
return "Review final intact tree";
