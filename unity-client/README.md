# Unity 6 Windows 客户端

此目录是“你家猫桌宠”的 Unity 6.3 LTS 项目。

## 当前阶段

- `Assets/Editor/StandardCatBuilder.cs`：生成版权完全自有的标准猫原型 Prefab、动画和控制器。
- `Assets/Scripts/CatMorphController.cs`：已绑定躯干、头、耳朵与四肢，支持胖瘦、脸宽、耳朵、腿长参数。
- `Assets/Scripts/DesktopPetBehaviour.cs`：桌面行走、拖拽以及坐、趴、睡、抚摸、喂食行为入口。
- `Assets/Scripts/DesktopWindowController.cs`：Windows DWM Alpha 透明、无边框置顶、点击穿透和高 DPI 命中转换。
- `Assets/Scripts/CatPackageManifest.cs`：云端猫模型包的数据格式。

## 当前动画

- `Idle.anim`：呼吸和尾巴轻摆。
- `Walk.anim`：四肢交替、身体起伏和尾巴摆动。
- `Sit.anim`：身体下沉、后腿折叠。
- `LieDown.anim`：身体下沉、前后腿收拢。
- `Sleep.anim`：趴下后的呼吸、头部和尾巴轻动。
- `Petted.anim`：抬头和摇尾反馈。
- `Eat.anim`：低头进食反馈。

## 生成标准猫

编辑器安装完成后，项目会通过批处理执行：

`YourCat.DesktopPet.Editor.StandardCatBuilder.Create`

输出：`Assets/StandardCat/StandardCat.prefab`

这个 Prefab 用于验证客户端、形态参数和动画管线，不代表最终照片拟合的毛绒品质。生产模型将保留相同部件语义，再替换为连续蒙皮网格和定制纹理。

## Windows 桌宠构建

执行 `YourCat.DesktopPet.Editor.DesktopPetBuild.BuildWindows` 生成 Windows 程序。

- 使用 DWM 扩展客户区和透明 Alpha 后缓冲区，不使用容易残留背景色的色键透明。
- 关闭 Flip Model Swapchain，并固定使用 Direct3D 11，保证 Unity 6 的透明窗口合成。
- 鼠标位于猫外时窗口点击穿透，位于猫身上时可拖拽。
- 使用 `ScreenToClient` 和窗口 DPI 比例转换，支持 Windows 高 DPI 缩放。
- 左键拖拽；右键抚摸；Shift + 右键喂食。
- 猫会在桌面边缘折返，并随机坐下、趴下或睡觉。
- Esc 退出桌宠。

## 托盘与设置

- 系统托盘右键菜单：打开设置、暂停/继续、快速调整大小、退出。
- 设置面板：胖瘦、脸宽、耳朵、腿长、整体大小和移动速度。
- 点击“应用并保存”后使用 `PlayerPrefs` 保存在当前 Windows 用户配置中。
- “登录 Windows 后自动启动”写入当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，不需要管理员权限。
- 当前 V13 已通过 Unity 编译和 Windows Player 构建，但按要求未做运行时托盘与面板测试。

## 安装包

`installer/DesktopCat.nsi` 使用开源 NSIS 3.12 将 V13 打包成当前用户安装程序。

- 默认安装至 `%LOCALAPPDATA%\Programs\YourCatDesktopPet`，无需管理员权限。
- 创建开始菜单快捷方式，可选创建桌面快捷方式。
- 注册到 Windows“已安装的应用”，支持完整卸载。
- 安装包输出至 `dist/YourCatDesktopPet-Setup-0.1.0.exe`。
