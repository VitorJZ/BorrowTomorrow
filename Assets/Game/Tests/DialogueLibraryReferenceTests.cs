using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BorrowTomorrow.Core.Data;
using NUnit.Framework;
using UnityEditor;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// 防止对白库再次被安装器整体覆盖：场景里引用的 DialogueEntry 必须都在库里，
    /// 库内每条 nextId 也必须能在库内解析，否则运行时会“第一句之后找不到下一句”。
    /// </summary>
    public sealed class DialogueLibraryReferenceTests
    {
        private const string Bootstrap = "Assets/Game/Scenes/Bootstrap.scene";
        private const string Scenes = "Assets/Game/Scenes";

        [Test]
        public void EverySceneDialogueReference_AndEveryChainLink_IsRegistered()
        {
            var libraryIds = LoadLibraryIds();
            Assert.IsNotEmpty(libraryIds, "Bootstrap 的 DialogueLibrary 为空");

            var entryByGuid = new Dictionary<string, DialogueEntry>();
            foreach (string guid in AssetDatabase.FindAssets("t:DialogueEntry"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var entry = AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);
                if (entry != null) entryByGuid[guid] = entry;
            }

            var problems = new List<string>();
            foreach (DialogueEntry entry in entryByGuid.Values)
            {
                if (!string.IsNullOrEmpty(entry.nextId) && !libraryIds.Contains(entry.nextId))
                    problems.Add($"{entry.entryId} -> {entry.nextId} 不在对白库中");
            }

            foreach (string sceneGuid in AssetDatabase.FindAssets("t:Scene", new[] { Scenes }))
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                string text = File.ReadAllText(scenePath);
                foreach (var pair in entryByGuid)
                {
                    if (!text.Contains(pair.Key) || libraryIds.Contains(pair.Value.entryId)) continue;
                    problems.Add($"{Path.GetFileName(scenePath)} 引用 {pair.Value.entryId}，但库里没有");
                }
            }

            CollectionAssert.IsEmpty(problems, string.Join("\n", problems.Distinct()));
        }

        private static HashSet<string> LoadLibraryIds()
        {
            string text = File.ReadAllText(Bootstrap);
            int start = text.IndexOf("\n  entries:", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Bootstrap 中找不到 DialogueLibrary.entries");
            int end = text.IndexOf("\n  actions:", start, System.StringComparison.Ordinal);
            Assert.Greater(end, start, "Bootstrap 中找不到 DialogueLibrary.actions 边界");
            string block = text.Substring(start, end - start);
            var ids = new HashSet<string>();
            foreach (Match match in Regex.Matches(block, @"guid: ([0-9a-f]{32})"))
            {
                string path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                var entry = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);
                Assert.IsNotNull(entry, "对白库引用了不存在的资产: " + match.Groups[1].Value);
                if (entry != null) ids.Add(entry.entryId);
            }
            return ids;
        }
    }
}
