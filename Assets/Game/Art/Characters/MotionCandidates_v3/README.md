# 动作审阅候选（未接入游戏）

- Jump：新 12 帧动作、完整 AnimationClip，以及 Prepare/Rise/Apex/Fall/Land 五段原生动画资产。保持统一像素比例，不将蹲下和收腿姿势缩放成站立高度。接入时必须由真实运动阶段驱动，不能照旧仅选固定一帧。
- 登台已按用户要求移除；原始文件移到项目外备份，不再生成登台资产。
- 旧 ClimbRig 拼接草稿已否决并移出活动资产。新的完整人物逐帧候选在相邻 ClimbFrames_v4 目录。

本目录仅保留跳跃独立审阅资产，不引用到主角 prefab。用户已确认的 WalkCandidate_v3 是单独的已接入行走动画。

生成入口：BorrowTomorrow.EditorTools.MotionCandidateInstaller.Create。
MOTION_CANDIDATE_OUTPUT 指定审阅导出目录。脚本仅生成跳跃动画资产和 unitypackage，不运行关卡。
