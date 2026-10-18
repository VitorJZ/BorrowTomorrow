# 原12帧跳跃：落地末段补两帧

用户选择上一版12帧跳跃作为基础。此次只在原第11帧最深屈膝与原第12帧站直之间加入两张完整人物姿态：半起身、微屈膝。共14帧，没有拆分人物身体部件。

- Keys_12.png：上一版源图原始字节。
- Recovery_2.png：新绘制的两张完整人物过渡姿态。
- manifest.json：完整人物Sprite矩形、位置支点、缩放比例和每帧时长。
- Frame_00—Frame_13：14张完整人物Sprite资产。
- Jump_Full_14_Review.anim：完整审阅循环，保留原帧播放时序，新增两帧各80ms。
- Jump_Land_Recovery.anim：落地至站稳末段，供后续确认接入时使用。
- ReviewOnly Prefab：独立原生Animator预览，无主角控制器。

GIF/HTML保留原12帧预览画面，原地插入两张新帧，不沿用已否决的24帧预览。HTML提供只看落地末段、慢放和逐帧功能。
用户已确认并要求接入游戏。Player_Graybox.prefab绑定14帧及对应五段动画，真实位移由PlayerMotor2D驱动；预览示意腾空位移不烘焙进游戏动画。

入口：BorrowTomorrow.EditorTools.JumpRecoveryInstaller.Create。
JUMP_RECOVERY_OUTPUT 指定unitypackage导出目录。

游戏接入菜单：BorrowTomorrow → 接入已确认的走路与14帧跳跃。
游戏打包入口：BorrowTomorrow.EditorTools.JumpRecoveryInstaller.IntegrateAndBuild。
