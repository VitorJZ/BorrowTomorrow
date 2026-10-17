using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Core.Interfaces;
using UnityEngine;

namespace BorrowTomorrow.Level
{
    /// <summary>
    /// 提示导演（策划 10.4 HintDirector + 12.4）：
    /// 进入新阻塞点后计时；长时间未推进时显示“H 查看提示”；
    /// 只有玩家按 H 才展开下一层，不用对白自动报答案。
    /// 提示阶段绑定 stageKey（剧情标记），阶段完成后停止旧提示计时。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HintDirector : MonoBehaviour
    {
        [Tooltip("无推进多久后出现“H 查看提示”（秒）")]
        [SerializeField] private float availabilityDelay = 22f;

        [Tooltip("显示提示气泡时长（秒）")]
        [SerializeField] private float availabilityDuration = 4f;

        private LevelDefinition definition;
        private StoryFlagStore flags;
        private float stagnationTimer;
        private int hintLevel;
        private bool availabilityShown;

        private void Start()
        {
            definition = GetComponentInParent<LevelRoot>()?.Definition;
            if (GameSession.Instance != null)
            {
                flags = GameSession.Instance.StoryFlags;
                flags.FlagAdded += OnFlagAdded;
            }
            ResetForActiveStage();
        }

        private void OnDestroy()
        {
            if (flags != null)
            {
                flags.FlagAdded -= OnFlagAdded;
            }
        }

        private void OnFlagAdded(string flagId)
        {
            // 阶段完成：停止旧提示计时并切换到下一阶段（策划 12.4）。
            ResetForActiveStage();
        }

        private HintGroup FindActiveStage()
        {
            if (definition == null || definition.hintGroups == null || flags == null)
            {
                return null;
            }
            foreach (HintGroup group in definition.hintGroups)
            {
                if (group == null || string.IsNullOrEmpty(group.stageKey))
                {
                    continue;
                }
                if (!flags.HasFlag(group.stageKey))
                {
                    return group;
                }
            }
            return null; // 全部阶段完成
        }

        private void ResetForActiveStage()
        {
            stagnationTimer = 0f;
            hintLevel = 0;
            availabilityShown = false;
        }

        private void Update()
        {
            if (GameSession.Instance == null || GameSession.Instance.State.Phase != GamePhase.Playing
                || GameSession.Instance.State.IsPaused)
            {
                return;
            }
            HintGroup stage = FindActiveStage();
            if (stage == null)
            {
                return;
            }

            stagnationTimer += Time.deltaTime;
            if (ServiceRegistry.TryGet<IInputSource>(out var input) && input.HintPressedThisFrame)
            {
                AdvanceHint(stage);
                return;
            }

            if (!availabilityShown && stagnationTimer >= availabilityDelay)
            {
                availabilityShown = true;
                if (ServiceRegistry.TryGet<IHudService>(out var hud))
                {
                    hud.ShowHintAvailable(stage.stageKey);
                }
            }
        }

        private void AdvanceHint(HintGroup stage)
        {
            string line = hintLevel switch
            {
                0 => stage.hintLevel1,
                1 => stage.hintLevel2,
                _ => stage.hintLevel3,
            };
            hintLevel = Mathf.Min(hintLevel + 1, 2);
            if (ServiceRegistry.TryGet<IHudService>(out var hud) && !string.IsNullOrEmpty(line))
            {
                hud.ShowHintText(line);
            }
            stagnationTimer = 0f; // 按 H 后重新计时，避免提示轰炸
            availabilityShown = false;
        }
    }
}
