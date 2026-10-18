# 走路动画 v3（已确认并接入）

用户于 2026-10-18 确认循环预览后，同一组 Sprite 和原生 AnimationClip 接入主角行走状态。保留审阅用 AnimatorController 与独立 Prefab。

- `Hero_Walk_Candidate_v3.png`：新的 8 帧候选图，保留原始 Alpha。
- `walk_candidate.json`：按实际人物轮廓切帧，统一人物高度，以头部/身体轴线定位，不跟随交替支撑脚变更中心。
- `Frames`：独立 Sprite 资产。
- `Hero_Walk.anim`：10 fps 循环 AnimationClip。
- `Hero_Walk.controller`：单 Walk 状态的原生 AnimatorController。
- `Hero_Walk_ReviewOnly.prefab`：仅 SpriteRenderer 与 Animator，无游戏控制器。

生成菜单：BorrowTomorrow → 制作独立走路候选资产（不接入游戏）。
游戏中的 CharacterArtAnimator 负责状态选择与计时，行走时采样 Hero_Walk.anim；不会同时启用第二个 Animator 写入同一 SpriteRenderer。其他动作仍使用原有帧，旧行走原图保留。
