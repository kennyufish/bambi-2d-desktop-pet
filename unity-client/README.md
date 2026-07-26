# Unity 6 客户端

该目录用于 Windows 10/11 透明置顶 3D 桌宠客户端。

当前仓库所在机器尚未安装 Unity 6，因此这里只提交客户端接口和体型参数逻辑，未生成或伪造 Unity 场景与模型资产。下一步需要：

1. 通过 Unity Hub 安装 Unity 6 LTS，并添加 Windows Build Support。
2. 用 Unity Hub 在此目录创建或打开项目。
3. 导入一套授权清晰、带统一骨架的标准猫模型。
4. 将模型的 Blend Shape 或骨骼缩放目标绑定到 `CatMorphController`。
5. 导入走、坐、趴、睡等动画并建立 Animator Controller。

资源包接口约定见 `Assets/Scripts/CatPackageManifest.cs`。
