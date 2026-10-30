using System.Collections.Generic;
using System.Linq;
using BorrowTomorrow.Core.Data;
using NUnit.Framework;
using UnityEditor;

namespace BorrowTomorrow.Tests
{
    /// <summary>
    /// L03 照片揭示 v11：在照片识别与“我是她外婆啊”之间插入记忆回声，
    /// 复用 L01B/L02 已听过的原声与插画，并保留病房袖口回收。
    /// </summary>
    public sealed class L03MemoryEchoTests
    {
        private const string V5Dir = "Assets/Game/Data/Dialogue/L03_Identity_v5/";
        private const string V11Dir = "Assets/Game/Data/Dialogue/L03_Identity_v11/";

        private static readonly string[] ExpectedChain =
        {
            "V5_L03_001",
            "V10_L03_001A",
            "V5_L03_002",
            "V5_L03_003",
            "V11_L03_ECHO_HEIGHT_01",
            "V11_L03_ECHO_HEIGHT_02",
            "V11_L03_ECHO_CUFF_01",
            "V11_L03_ECHO_CUFF_02",
            "V11_L03_ECHO_LEDGER_01",
            "V5_L03_004",
            "V5_L03_005",
            "V5_L03_006",
            "V5_L03_007",
            "V5_L03_008",
            "V5_L03_009",
            "V11_L03_CUFF_HOSPITAL_01",
            "V5_L03_012",
            "V5_L03_013",
            "V5_L03_014",
            "V5_L03_015",
            "V5_L03_016",
            "V5_L03_017",
        };

        private static readonly string[] MemoryEchoIds =
        {
            "V11_L03_ECHO_HEIGHT_01",
            "V11_L03_ECHO_HEIGHT_02",
            "V11_L03_ECHO_CUFF_01",
            "V11_L03_ECHO_CUFF_02",
            "V11_L03_ECHO_LEDGER_01",
        };

        private static readonly string[] V11Ids =
        {
            "V11_L03_ECHO_HEIGHT_01",
            "V11_L03_ECHO_HEIGHT_02",
            "V11_L03_ECHO_CUFF_01",
            "V11_L03_ECHO_CUFF_02",
            "V11_L03_ECHO_LEDGER_01",
            "V11_L03_CUFF_HOSPITAL_01",
        };

        private static DialogueEntry Load(string id)
        {
            var entry = AssetDatabase.LoadAssetAtPath<DialogueEntry>(V5Dir + id + ".asset");
            return entry != null ? entry : AssetDatabase.LoadAssetAtPath<DialogueEntry>(V11Dir + id + ".asset");
        }

        private static List<DialogueEntry> Chain(string id)
        {
            var result = new List<DialogueEntry>();
            var visited = new HashSet<string>();
            while (!string.IsNullOrEmpty(id))
            {
                Assert.IsTrue(visited.Add(id), "Cyclic dialogue: " + id);
                var entry = Load(id);
                Assert.IsNotNull(entry, "Missing dialogue: " + id);
                result.Add(entry);
                id = entry.nextId;
            }
            return result;
        }

        [Test]
        public void PhotoChain_MatchesV11Order()
        {
            var chain = Chain("V5_L03_001");
            CollectionAssert.AreEqual(ExpectedChain, chain.Select(e => e.entryId).ToArray());
            Assert.AreEqual(22, chain.Count);
        }

        [Test]
        public void MemoryEcho_LinesAutoAdvanceWithExpectedVoices()
        {
            foreach (string id in MemoryEchoIds)
            {
                var entry = Load(id);
                Assert.IsNotNull(entry, id);
                Assert.IsTrue(entry.autoAdvance, id + " 必须自动推进");
                Assert.IsFalse(entry.holdForConfirm, id + " 不等待玩家确认");
            }

            foreach (string id in MemoryEchoIds.Where(i => i != "V11_L03_ECHO_LEDGER_01"))
            {
                Assert.IsNotNull(Load(id).voiceClip, id + " 必须复用已听过的原声");
            }

            var ledger = Load("V11_L03_ECHO_LEDGER_01");
            Assert.IsNull(ledger.voiceClip, "账本句无配音，靠字幕与停留");
            Assert.GreaterOrEqual(ledger.minDurationSeconds, 3f, "账本句需要足够阅读时间");
        }

        [Test]
        public void MemoryEcho_MarksTheReplaySegmentAndCuesTapeAudio()
        {
            foreach (string id in MemoryEchoIds)
                Assert.IsTrue(Load(id).memoryEcho, id + " 必须标记为记忆回放");
            Assert.AreEqual("sfx_tape_click", Load("V11_L03_ECHO_HEIGHT_01").sfxId, "段首用磁带按键声提示回放开始");
            Assert.AreEqual("sfx_cloth_paper", Load("V11_L03_ECHO_LEDGER_01").sfxId, "账本句用翻纸声");
            Assert.IsFalse(Load("V11_L03_CUFF_HOSPITAL_01").memoryEcho, "病房袖口回收属于现实，不加回忆滤镜");
            Assert.IsFalse(Load("V5_L03_004").memoryEcho, "结论句回到现实照片");
        }

        [Test]
        public void MemoryEcho_UsesPreviouslySeenIllustrations()
        {
            Assert.AreEqual("ILL_HEIGHT_TIPTOE", Load("V11_L03_ECHO_HEIGHT_01").illustration.name);
            Assert.AreEqual("ILL_HEIGHT_TIPTOE", Load("V11_L03_ECHO_HEIGHT_02").illustration.name);
            Assert.AreEqual("ILL_UNIFORM_CUFF", Load("V11_L03_ECHO_CUFF_01").illustration.name);
            Assert.AreEqual("ILL_UNIFORM_CUFF", Load("V11_L03_ECHO_CUFF_02").illustration.name);
            Assert.AreEqual("ILL_L03_Ledger_Note", Load("V11_L03_ECHO_LEDGER_01").illustration.name,
                "账本句使用可读的真实账本特写，不再使用 AI 伪字");
            Assert.AreEqual("ILL_L03_CUFF_HOSPITAL", Load("V11_L03_CUFF_HOSPITAL_01").illustration.name,
                "病房袖口回收要用手摸着袖口的病房特写，不能复用缝纫桌插画");
        }

        [Test]
        public void V11Entries_DoNotCarryStoryFlags()
        {
            foreach (string id in V11Ids)
            {
                var entry = Load(id);
                Assert.IsEmpty(entry.requiredFlags ?? new string[0], id + " 的 requiredFlags 必须留空");
                Assert.IsEmpty(entry.setFlags ?? new string[0], id + " 的 setFlags 必须留空");
                Assert.IsEmpty(entry.completionFlags ?? new string[0], id + " 的 completionFlags 必须留空");
                Assert.IsEmpty(entry.objectiveAfter ?? string.Empty, id + " 不得改写目标");
            }
        }

        [Test]
        public void IdentityFlags_StayOnHeadAndTail()
        {
            CollectionAssert.Contains(Load("V5_L03_001").completionFlags, "PhotoRevealed");
            CollectionAssert.Contains(Load("V5_L03_001").completionFlags, "IdentityKnown");
            CollectionAssert.Contains(Load("V5_L03_017").setFlags, "PhotoRevealed");
            CollectionAssert.Contains(Load("V5_L03_017").setFlags, "IdentityKnown");
        }

        [Test]
        public void RemovedLines_AreNotOnChain()
        {
            var chain = Chain("V5_L03_001").Select(e => e.entryId).ToList();
            CollectionAssert.DoesNotContain("V5_L03_010", chain);
            CollectionAssert.DoesNotContain("V5_L03_011", chain);
        }

        [Test]
        public void HospitalCuffCallback_UsesOriginalVoice()
        {
            var entry = Load("V11_L03_CUFF_HOSPITAL_01");
            Assert.IsNotNull(entry.voiceClip);
            Assert.AreEqual("vo_l03_reveal_03", entry.voiceClip.name);
            Assert.IsFalse(entry.autoAdvance);
            Assert.IsTrue(entry.holdForConfirm);
        }

        [Test]
        public void HospitalCallbacks_UseL00NoticeAndClothMemory()
        {
            Assert.AreEqual("ILL_L00_NOTICE", Load("V5_L03_012").illustration.name);
            Assert.AreEqual("ILL_ClothMemory", Load("V5_L03_013").illustration.name);
        }
    }
}
