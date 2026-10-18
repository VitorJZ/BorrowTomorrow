using UnityEditor;
var gt=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var gw=EditorWindow.GetWindow(gt);var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
gt.GetProperty("selectedSizeIndex",f).SetValue(gw,SessionState.GetInt("ArtSamplePreviousView",0));
EditorApplication.isPlaying=false;
return "Stopped review and restored original Game View option";
