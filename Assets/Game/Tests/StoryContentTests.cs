using System.Collections.Generic;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using NUnit.Framework;
using UnityEditor;

namespace BorrowTomorrow.Tests
{
    public sealed class StoryContentTests
    {
        private static List<DialogueEntry> Chain(string id)
        {
            var result = new List<DialogueEntry>(); var visited = new HashSet<string>();
            while (!string.IsNullOrEmpty(id))
            {
                Assert.IsTrue(visited.Add(id), "Cyclic dialogue: " + id);
                var entry = AssetDatabase.LoadAssetAtPath<DialogueEntry>("Assets/Game/Data/Dialogue/D_" + id + ".asset");
                Assert.IsNotNull(entry, "Missing dialogue: " + id);
                result.Add(entry); id = entry.nextId;
            }
            return result;
        }
        [TestCase("intro_kid", StoryFlags.IntroPlayed)]
        [TestCase("tree_marks", StoryFlags.HeightMarksSeen)]
        [TestCase("uniform_self", StoryFlags.UniformRead)]
        [TestCase("photo_back", StoryFlags.IdentityKnown)]
        [TestCase("compartment_touch", StoryFlags.BoxLocated)]
        [TestCase("epilogue_letter", StoryFlags.EpilogueSeen)]
        public void StoryFlag_IsWrittenOnlyAtEndOfCompleteChain(string id, string flag)
        {
            var chain = Chain(id);
            Assert.Greater(chain.Count, 1);
            foreach (var line in chain.Take(chain.Count - 1)) CollectionAssert.DoesNotContain(line.setFlags, flag);
            CollectionAssert.Contains(chain.Last().setFlags, flag);
        }
        [Test] public void LetterAndRealityDelivery_AreExplicit()
        {
            string letter = string.Concat(Chain("epilogue_letter").Select(e => e.subtitle)).Replace("\n", "");
            StringAssert.Contains("不用急着回来，把自己的日子过好。", letter);
            StringAssert.Contains("明天还来吗？", letter);
            string arrival = string.Concat(Chain("epilogue_arrival").Select(e => e.subtitle));
            StringAssert.Contains("两天后", arrival);
            Assert.IsNotNull(Chain("epilogue_arrival").Last().illustration,
                "现实交付由铁盒插画呈现，不新增旁白台词");
        }
        [Test] public void EndingCannotBeExitedBeforeReading()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Data/Levels/Epilogue_Epilogue.asset");
            CollectionAssert.Contains(level.exitRequiredFlags, StoryFlags.EpilogueSeen);
        }
    }
}
