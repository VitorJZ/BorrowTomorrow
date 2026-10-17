# 借一点明天（BorrowTomorrow）

> 2D 横版叙事解谜闯关游戏 · 团结引擎（Tuanjie Engine）· 比赛项目

## 一句话玩法

拖动一条竖直时间边界，让左侧成为 1994 年、右侧成为 2024 年，借两个年代的道路抵达阁楼。

## 项目信息

| 项 | 值 |
| --- | --- |
| 引擎 | 团结引擎（Tuanjie）1.10.4（内部版本 2022.3.62t16） |
| 模板 | 2D（内置渲染管线，cn.tuanjie.template.2d 7.0.4） |
| 渲染 | 正交摄像机 + SpriteRenderer + Sprite Mask（见 `Docs/02_技术架构与模块设计.md`） |
| 目标平台 | Windows 桌面（优先） |
| 目标时长 | 初见约 25—35 分钟（以 30 分钟为节奏目标） |
| 设计基线 | `Docs/Design/借一点明天_2D团结引擎游戏策划方案_v1.0.txt` |

## 快速开始

1. 用团结 Hub 打开本项目（编辑器版本 **1.10.4**，勿用其他版本打开——策划方案 10.1 要求锁定同一编辑器版本）。
2. 首次打开后如需重建场景骨架，执行菜单 **BorrowTomorrow → 一键搭建项目骨架**（可重复运行，幂等）。
3. 从 **Assets/Game/Scenes/Bootstrap.scene** 进入 Play 模式。
4. 键位（策划 5.1）：`A/D` 移动、`Space` 跳、`W/S` 爬梯、`E` 交互、`Q/R` 或顶部把手调整时间边界、`Backspace` 恢复检查点（需二次确认）、`H` 提示、`Tab` 查看目标、`Esc` 暂停。

## 运行测试

团结引擎菜单 **Window → General → Test Runner**（EditMode），或命令行：

```powershell
& "C:\Program Files\Tuanjie\Hub\Editor\2022.3.62t16\Editor\Tuanjie.exe" -batchmode -quit `
  -projectPath "<项目路径>" `
  -runTests -testPlatform EditMode -testResults "Tests/results.xml" `
  -logFile "Tests/runtests.log"
```

覆盖面（策划 15.5：少量核心自动测试）：边界阈值排序与分组、安全阻挡（覆盖/支撑撤除）、存档序列化往返与备份恢复、故事标记幂等性、状态机暂停恢复。

## 目录速览

```
Assets/
  Game/
    Scenes/        Bootstrap、MainMenu、L00–L04、Epilogue（一键脚手架生成）
    Scripts/
      Core/        游戏会话、状态机、服务注册表、跨模块接口、数据定义
      Player/      输入路由、角色运动、攀爬、交互
      TimeBoundary/ 时间边界、年代物体、安全校验、视觉遮罩
      Level/       房间根、出口、检查点、跌落区、提示、相机
      Narrative/   对白播放、剧情导演、调查物、区域触发
      Audio/       音频服务、年代环境声、音效库
      Save/        存档服务、检查点服务
      UI/          HUD、边界把手、字幕、暂停菜单、主菜单
      EditorTools/ 项目脚手架（仅编辑器）
    Data/          LevelDefinition、TemporalObjectDefinition、DialogueEntry 资产
    Art/ Audio/ Prefabs/ Tests/
Docs/              开发文档（本目录）
```

## 文档索引

| 文档 | 内容 |
| --- | --- |
| [01_项目概述与设计支柱](Docs/01_项目概述与设计支柱.md) | 游戏定位、体验目标、核心规则、明确不做清单 |
| [02_技术架构与模块设计](Docs/02_技术架构与模块设计.md) | 程序集划分、组件职责、数据流、核心算法 |
| [03_模块开发指南](Docs/03_模块开发指南.md) | 各模块文件清单与扩展步骤（新增关卡/物体/对白） |
| [04_场景搭建指南](Docs/04_场景搭建指南.md) | 系统根与关卡场景结构、排序层、物理层、标准配置 |
| [05_Git工作流与团队协作](Docs/05_Git工作流与团队协作.md) | 分支模型、模块负责人、提交规范、场景冲突规避 |
| [06_编码规范](Docs/06_编码规范.md) | 命名、程序集规则、注释与数据驱动约定 |
| [07_里程碑与验收清单](Docs/07_里程碑与验收清单.md) | 阶段 A–E、验收与试玩方法 |
| [08_设计决策记录](Docs/08_设计决策记录.md) | 关键技术选型与理由（ADR） |
| [Design/](Docs/Design/) | 策划方案原文归档（v1.0，2026-10-17） |

## 团队分工（策划 13.2）

- **策划 / 叙事**：锁定关卡状态表、对白资产（`DialogueEntry`）、触发条件、谜题验收。
- **程序**：输入、运动、时间系统、碰撞安全、加载、对白、存档、打包。
- **美术**：双年代资源（`Art/`）、角色动画、插画、UI、层级可读性。
- **音频**：配音、环境音、音乐混合（`Audio/`、`SfxLibrary`）。

同一关卡场景约定主要编辑者，减少场景合并冲突（详见 05 文档）。
