# MapGenerator.Core

MapGenerator.Core 是一个用于生成红色警戒2随机地图和提取地图缩略图的类库。

## 功能特性

- 生成随机地图（支持不同气候、玩家数量和地图大小）
- 从地图文件中提取缩略图
- 启动FinalAlert2SP地图编辑器

## 目录结构

```
MapGenerator.Core/
├── MapGenerator.Core.csproj    # 项目文件
├── RandomMapGenerator.cs        # 随机地图生成逻辑
├── MapPreviewExtractor.cs       # 地图缩略图提取逻辑
├── MapEditorLauncher.cs         # 地图编辑器启动逻辑
└── README.md                    # 本文档

MapGenerator.Example/
├── MapGenerator.Example.csproj  # 示例应用程序项目文件
└── Program.cs                   # 示例代码
```

## 如何集成到现有项目

1. 将 MapGenerator.Core 项目添加到你的解决方案中
2. 在你的项目中添加对 MapGenerator.Core 的引用
3. 安装必要的依赖项：
   - SixLabors.ImageSharp (用于图像处理)

## 如何使用

### 1. 生成随机地图

```csharp
using MapGenerator.Core;

// 创建随机地图生成器
RandomMapGenerator generator = new RandomMapGenerator("E:\\Games\\RedAlert2\\");

// 设置生成选项
RandomMapOptions options = new RandomMapOptions
{
    Climate = "TEMPERATE",      // 气候：TEMPERATE, TEMPERATE_Islands, NEWURBAN, DESERT, Random
    PlayerCount = 2,             // 玩家数量：2-8
    Size = 1,                    // 地图大小：0=small, 1=medium, 2=big, 3=very big
    RandomBuildingDamage = true  // 是否启用随机建筑损伤
};

// 生成随机地图
generator.GenerateRandomMap(options);

// 生成的地图文件位于：Maps/Custom/随机地图.map
// 生成的缩略图位于：Maps/Custom/随机地图.png
```

### 2. 提取地图缩略图

```csharp
using MapGenerator.Core;
using SixLabors.ImageSharp;

// 从地图文件中提取缩略图
Image image = MapPreviewExtractor.ExtractMapPreview("E:\\Games\\RedAlert2\\Maps\\Custom\\随机地图.map");

if (image != null)
{
    // 保存提取的缩略图
    image.Save("E:\\Games\\RedAlert2\\Maps\\Custom\\extracted_preview.png");
}
```

### 3. 启动FinalAlert2SP地图编辑器

```csharp
using MapGenerator.Core;

// 创建地图编辑器启动器
MapEditorLauncher launcher = new MapEditorLauncher("E:\\Games\\RedAlert2\\");

// 启动FinalAlert2SP
launcher.LaunchFinalAlert2SP();
```

## 注意事项

1. **依赖项**：
   - 随机地图生成功能依赖于 RandomMapGenerator.exe 和 CNCMaps.Renderer.exe
   - 这些工具应该位于游戏目录的 Resources\RandomMapGenerator_RA2 文件夹中

2. **文件路径**：
   - 确保游戏路径设置正确
   - 确保 Maps\Custom 文件夹存在，用于存储生成的地图和缩略图

3. **权限**：
   - 应用程序需要有写入 Maps\Custom 文件夹的权限

4. **性能**：
   - 生成随机地图可能需要一些时间，建议在后台线程中执行

## 示例应用程序

MapGenerator.Example 是一个控制台应用程序，演示了如何使用 MapGenerator.Core 的所有功能。

### 运行示例

1. 打开 MapGenerator.Example.csproj 项目
2. 修改 Program.cs 中的 gamePath 变量为你的游戏路径
3. 编译并运行示例应用程序
4. 按照控制台提示选择要执行的操作

## 技术细节

- **随机地图生成**：使用 RandomMapGenerator.exe 生成地图，然后使用 CNCMaps.Renderer.exe 生成缩略图
- **缩略图提取**：从地图文件的 [PreviewPack] 部分提取压缩的预览数据，解压缩后转换为图像
- **地图编辑器启动**：通过命令行启动 FinalAlert2SP.exe

## 支持的游戏版本

- 红色警戒2
- 尤里的复仇
- 基于红色警戒2引擎的mod（如DTA）
