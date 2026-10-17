# 05 Git 工作流与团队协作

> 本项目按模块划分程序集与目录（策划 10.2 / 13.2），
> Git 分支与提交也按模块组织，让团队可以并行开发、按模块追溯。

## 1. 仓库基线

- 仓库根：项目根目录（`BorrowTomorrow/`，与 `Assets/`、`ProjectSettings/`、`Packages/` 同级）。
- `.gitignore`：忽略 `Library/ Temp/ Obj/ Logs/ UserSettings/ Build*/ *.csproj *.sln` 等（团结/Unity 标准）。
- **必须提交**：`Assets/`（含每个文件与目录的 `.meta`）、`Packages/manifest.json`、`ProjectSettings/`、`Docs/`。

## 2. .meta 文件规则（重要）

- 团结引擎用 `.meta` 的 GUID 追踪引用。**任何资产移动/重命名必须在编辑器内进行**（自动更新 meta）；不要在文件系统里直接移动 Assets 内文件。
- 新文件加入后**立即连同 `.meta` 一起提交**，避免他人打开项目时生成不同 GUID。
- 严禁把 `.meta` 加入 .gitignore。

## 3. 分支模型

```
main          稳定线：随时可打开可玩；只接受 dev 的合并与修复
  └─ dev     集成线：每日合并汇总；里程碑验收后合回 main
       ├─ feature/core-<主题>       程序（核心/时间系统/存档…）
       ├─ feature/level-<关卡>       关卡搭建（L00…L04、尾声）
       ├─ feature/narrative-<主题>   剧情对白
       ├─ feature/art-<主题>         美术资源
       └─ feature/audio-<主题>       音频
```

- 命名：`feature/<模块>-<短主题>`，修复 `fix/<模块>-<问题>`。
- 生命周期：从 `dev` 拉出 → 完成自测 → 合回 `dev`；里程碑（策划 14 章 A–E）验收后 `dev → main` 打 tag（`milestone-A` …）。
- 长期分支每天 rebase/merge `dev`，避免场景合并冲突堆积。

## 4. 提交规范

格式：`<类型>(<模块>): <一句话说明>`

| 类型 | 用途 |
| --- | --- |
| feat | 新功能/新内容 |
| fix | 修复 |
| art | 美术资源 |
| audio | 音频资源 |
| data | LevelDefinition / Dialogue / TemporalObject 数据资产 |
| docs | 文档 |
| chore | 项目设置/工具 |

模块限定词：`core / player / time / level / narrative / audio / save / ui / tests / tools`。

示例：

```
feat(time): 边界提交按阈值分组并接入安全校验
fix(save): 存档损坏时回落备份并修复正式文件
data(level): L02 出口条件绑定 UniformRead 标记
art(environment): 缝纫间 1994/2024 双年代背景
docs(guide): 补充场景搭建指南的防绕关清单
```

每次提交只动一个模块的内容；跨模块改动拆成多次提交（便于按模块追溯与回滚）。

## 5. 场景与 Prefab 冲突规避（策划 13.2）

- **同一关卡场景约定唯一主要编辑者**；其他人要改场景时先在群里登记。
- 场景无法文本级合并。改动场景时：先 pull → 改 → 立即 push；发现冲突时以"主要编辑者版本"为准，重做小改动。
- 能放 Prefab 的内容（年代物体、出口、检查点）都做成 Prefab（`Assets/Game/Prefabs/`），场景只留实例与覆盖值——Prefab 是 YAML 可读、冲突概率低。
- 大的关卡搭建提交前先在编辑器内运行一次本关。

## 6. 每日流程

1. `git pull origin dev`；
2. 在自己的 feature 分支上工作（编辑器内操作 Assets）；
3. 运行 Test Runner（EditMode）全绿；
4. 提交（`Assets` 变更与 `.meta` 成对）；
5. push 并向 `dev` 发起合并请求，说明改了哪个模块、影响哪些关卡。

## 7. 首次拉取后 checklist

- [ ] 团结 Hub 安装 **1.10.4** 编辑器（`tuanjie.exe editors list-releases` 可查）；
- [ ] 用 1.10.4 打开项目（版本不符禁止打开，避免 ProjectVersion 变更）；
- [ ] 首次打开后菜单 **BorrowTomorrow → 一键搭建项目骨架** 不需要运行（已生成），但可用来校验（幂等）；
- [ ] 从 `Assets/Game/Scenes/Bootstrap.scene` 进入 Play 验证。
