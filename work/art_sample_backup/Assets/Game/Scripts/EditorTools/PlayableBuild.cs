using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class PlayableBuild
    {
        [MenuItem("BorrowTomorrow/构建可玩 Windows 灰盒")]
        public static void BuildWindows()
        {
            ProjectScaffolder.RunFromCommandLine();
            string destination = Environment.GetEnvironmentVariable("BORROWTOMORROW_PLAYABLE_OUTPUT");
            if (string.IsNullOrEmpty(destination)) destination = Path.GetFullPath("Builds/Playable/BorrowTomorrow.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            bool previous = PlayerSettings.runInBackground;
            try
            {
                PlayerSettings.runInBackground = true;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    target = BuildTarget.StandaloneWindows64,
                    locationPathName = destination,
                    options = BuildOptions.Development,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Playable build failed: " + report.summary.result);
                Debug.Log("[PlayableBuild] Success: " + destination);
            }
            finally
            {
                PlayerSettings.runInBackground = previous;
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
            }
        }
    }
}
