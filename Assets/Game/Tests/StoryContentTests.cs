using System.Collections.Generic;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Narrative;
using NUnit.Framework;
using UnityEditor;

namespace BorrowTomorrow.Tests
{
    public sealed class StoryContentTests
    {
        private const string Dir = "Assets/Game/Data/Dialogue/StoryV13/";
        private static DialogueEntry Load(string id) => AssetDatabase.LoadAssetAtPath<DialogueEntry>(Dir + id + ".asset");
        private static List<DialogueEntry> Chain(string id)
        {
            var result = new List<DialogueEntry>(); var visited = new HashSet<string>();
            while (!string.IsNullOrEmpty(id))
            {
                Assert.IsTrue(visited.Add(id), "Cyclic dialogue: " + id);
                var entry = Load(id);
                Assert.IsNotNull(entry, "Missing dialogue: " + id);
                result.Add(entry); id = entry.nextId;
            }
            return result;
        }
        [Test] public void AllThirtyFiveEntries_KeepPerLineSetFlagsEmpty()
        {
            var all = AssetDatabase.FindAssets("t:DialogueEntry", new[] { Dir.TrimEnd('/') })
                .Select(g => AssetDatabase.LoadAssetAtPath<DialogueEntry>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.AreEqual(35, all.Count);
            foreach (var entry in all) Assert.IsEmpty(entry.setFlags ?? new string[0], entry.entryId + " 的逐句 setFlags 必须留空（规格 4.1）");
        }
        [TestCase("DLG_L00_INTRO_01", StoryFlags.IntroPlayed)]
        [TestCase("DLG_L01B_TREE_01", StoryFlags.HeightMarksSeen)]
        [TestCase("DLG_L02_UNIFORM_01", StoryFlags.UniformRead)]
        [TestCase("DLG_L03_REVEAL_01", StoryFlags.IdentityKnown)]
        [TestCase("DLG_L03_REVEAL_01", StoryFlags.PhotoRevealed)]
        [TestCase("DLG_L04_BOX_01", StoryFlags.BoxLocated)]
        [TestCase("DLG_EPI_01", StoryFlags.EpilogueSeen)]
        public void EventCompletion_IsConfiguredOnlyOnChainHead(string head, string flag)
        {
            var chain = Chain(head);
            CollectionAssert.Contains(chain[0].completionFlags, flag);
            foreach (var line in chain.Skip(1)) CollectionAssert.DoesNotContain(line.completionFlags ?? new string[0], flag);
        }
        [Test] public void LetterAndRealityDelivery_AreExplicit()
        {
            var chain = Chain("DLG_EPI_01");
            string text = string.Concat(chain.Select(e => e.subtitle)).Replace("\n", "");
            StringAssert.Contains("两天后", chain[0].subtitle);
            StringAssert.Contains("不用急着回来，把自己的日子过好。", text);
            StringAssert.Contains("明天还来吗？", text);
            Assert.IsNotNull(Load("DLG_EPI_02").illustration, "现实交付由铁盒插画呈现，不新增旁白台词");
        }
        [TestCase("ACT_L02_CUFF", "DLG_L02_UNIFORM_01", false, false)]
        [TestCase("ACT_L04_BOARD", "DLG_L04_BOX_01", false, true)]
        [TestCase("ACT_EPI_SMOOTH", "DLG_EPI_07", true, false)]
        public void ActionStages_GateTheSpecifiedLines(string actionId, string gate, bool confirmBefore, bool confirmAfter)
        {
            var action = AssetDatabase.LoadAssetAtPath<NarrativeAction>("Assets/Game/Data/Narrative/Actions/" + actionId + ".asset");
            Assert.IsNotNull(action, actionId);
            Assert.AreEqual(gate, action.beforeEntryId);
            Assert.AreEqual(confirmBefore, !string.IsNullOrEmpty(action.confirmBefore));
            Assert.AreEqual(confirmAfter, !string.IsNullOrEmpty(action.confirmAfter));
            Assert.IsNotNull(action.afterImage);
        }
        [Test] public void ReportedLocation_IsSpokenAtHospitalAfterBoardAction()
        {
            var board = AssetDatabase.LoadAssetAtPath<NarrativeAction>("Assets/Game/Data/Narrative/Actions/ACT_L04_BOARD.asset");
            Assert.AreNotEqual(board.afterImage, Load("DLG_L04_BOX_01").illustration, "拨板画面与病房报位置必须分开");
            StringAssert.Contains("右上边", Load("DLG_L04_BOX_01").subtitle);
        }
        [Test] public void EndingCannotBeExitedBeforeReading()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Data/Levels/Epilogue_Epilogue.asset");
            CollectionAssert.Contains(level.exitRequiredFlags, StoryFlags.EpilogueSeen);
        }
    }
}
