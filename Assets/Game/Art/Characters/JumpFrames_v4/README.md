# 完整人物跳跃补帧候选 v4

此24帧版本未获用户认可，保留为历史候选。后续采用上一版12帧作为基础，只在屈膝至站直之间补两帧，见相邻 JumpRecovery_v5；不要把本目录作为当前接入目标。

- 保留原有12张完整人物关键帧（Keys_12.png，原始字节未改动）。
- 新生成12张完整人物过渡帧（Inbetweens_12.png）。
- 原帧与新帧交替组成24帧，以24fps播放，完整时长1秒；没有拆分人物身体部件，没有骨骼或关节拼接。
- Frame_00—Frame_23 为完整人物Sprite资产。Jump_Full_24.anim 为完整动画，另有 Prepare/Rise/Apex/Fall/Land 五段动画。
- Visual 的整帧位置用于保持腾空时腰部高度，收腿时不会把整个人缩成更矮的角色；地面阶段脚底位置固定。
- Prefab仅供独立审阅，不包含主角控制器。动画尚未接入游戏，仍需用户确认体型、姿态衔接和节奏。

重新生成资产菜单：BorrowTomorrow → 制作完整人物24帧跳跃候选（不接入游戏）。
命令入口 BorrowTomorrow.EditorTools.JumpFrameInstaller.Create，JUMP_FRAMES_OUTPUT 指定输出目录。

HTML/GIF预览含示意腾空轨迹；原生AnimationClip只记录完整人物姿态，真实位移应在后续接入时由游戏物理驱动。
