# 已确认的24帧跳跃 v6

角色完整重绘的24帧序列，参考 Flashback 的动作阶段。

- SourceFrames：原始完整人物透明PNG；Source_24_final.png：整张生成源图。
- Frames：完整人物只做透明留白与位置对齐，未拆分四肢、未变形、未缩放单帧。
- Sprites：Unity Sprite资产；五段原生.anim由JumpReferenceV6Installer生成。
- 固定缩放和坐标原点；游戏物理决定腾空位移，图片没有烘焙预览的上升轨迹。
- 地面蓄力0.18秒，宽容跳与梯子跳仍立即施加冲量。
- 静止落地播放全部恢复帧，横向移动沿用可打断恢复的控制方式。
- 原JumpRecovery_v5资产完整保留。
