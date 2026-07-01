# DTA.CLI 设计文档 — Spawn 配置生成器

版本：1.0

作者：开发助理
位置：Tools/DTA.CLI

概述
---
本工具的唯一职责是从地图文件生成游戏所需的两份 INI：`spawnmap.ini` 与 `spawn.ini`。该工具设计为轻量级 CLI，避免直接依赖客户端或渲染工具，便于脚本化与集成流水线。

目标
---
- 提供可重复、可自动化的 spawn 文件生成流程。
- 保证生成的 INI 格式与客户端期待的关键字段一致（Scenario, PlayerCount, OtherN 等）。
- 支持 `--dry-run` 和 `--out` 便于 CI/测试验证。

命令接口与行为
---
- `map:generate-spawnmap <mapFile> --game-path <dir> [--out <file>] [--dry-run]`
  - 输入：地图文件路径（通常为 `.map` 或 map.ini）。
  - 行为：读取 map 文件文本，保留原有内容，确保 `[Basic]` 段包含 `OriginalFilename=<mapFileName>`（若不存在则插入）；把结果写入目标 `spawnmap.ini`。
  - 输出位置：优先 `--out` 指定路径；否则写入 `<game-path>/spawnmap.ini`（`--game-path` 或 `--out` 必须提供其一）。
  - `--dry-run`：不写文件，仅把将写入的内容输出到 stdout（用于测试）。
  - 退出码：0 成功；1 地图文件不存在；2 参数错误（未指定输出路径）；3 写入失败；99 异常。

- `map:generate-spawnini <mapFile> --game-path <dir> --player "name,side,color[,start]" [--player ...] [--ai <n>] [--seed <n>] [--out <file>] [--dry-run]`
  - 输入：地图文件路径与至少一个 `--player` 参数。玩家参数可重复使用，格式 `name,side,color[,start]`（`start` 为可选起点索引，当前实现解析但未写入 spawn.ini）。
  - 行为：生成 `spawn.ini`，写入 `[Settings]` 段（`Name`、`Scenario=spawnmap.ini`、`UIMapName`、`PlayerCount`、主机 `Side`/`Color`、`AIPlayers`、`Seed` 等），以及 `OtherN` 段（每位其他玩家的 `Name`、`Side`、`Color`）。
  - 输出位置与 `--dry-run` 同上。
  - 退出码：同上。

实现要点
---
- Map 处理
  - 读取 map 文件为文本行：File.ReadAllLines(mapPath)。
  - 检查并定位 `[Basic]` 节；在该节内查找 `OriginalFilename` 键并替换或插入；若文件没有 `[Basic]` 节，则在文件顶部插入该节与键。

- Spawn.ini 生成
  - 主机（第一个 `--player`）信息写入 `[Settings]` 的 `Name`、`Side`、`Color`。
  - `UIMapName` 当前使用 `Path.GetFileNameWithoutExtension(mapPath)`（可改进为优先使用 map `[Basic]` 的 `Name` 字段）。
  - `PlayerCount` = 提供的玩家数量（不包含 AIPlayers 计数）；`AIPlayers` = `--ai` 参数值。
  - `Seed` 使用 `--seed` 或随机数生成。
  - 对于 i>=2 的玩家依次生成 `[Other1]`、`[Other2]` 节，包含 `Name`/`Side`/`Color`。

- 参数解析
  - 简单的命令行扫描实现（不使用外部 CLI 库），支持重复 `--player`。

- I/O
  - 写入时使用 UTF-8 编码并确保目标目录存在：Directory.CreateDirectory(Path.GetDirectoryName(target) ...)。

已实现（当前代码状态）
---
- map:generate-spawnmap：已实现，行为与文档一致 —— 在 `[Basic]` 中插入/替换 `OriginalFilename`，支持 `--out` 与 `--dry-run`。
- map:generate-spawnini：已实现，支持重复 `--player`、`--ai`、`--seed`、`--out` 与 `--dry-run`，会生成 `[Settings]` 与 `OtherN` 节。
- `--dry-run`：实现，输出将写入内容到 stdout，不做文件写入。
- 参数错误与退出码：实现了基本错误码（1/2/3/99），与上文约定一致。
- 构建配置：`DTA.CLI.csproj` 已设置 `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` 与 `<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>`，并已移除对 `Rampastring.Tools` 的直接依赖；项目可成功构建（存在若干 Code Analysis 警告）。

未实现 / 可改进项
---
- Supplemental 文件复制：当前实现不复制地图的 supplemental 资源（如自定义贴图、预览图）；若需支持请引用 `ClientCore/ClientConfiguration.cs` 中 `SupplementalMapFileExtensions` 并实现复制逻辑。
- `UIMapName` 数据来源：当前使用文件名，建议改为优先读取 map `[Basic]` 的 `Name` 字段（若存在）。
- `start` 字段：`--player` 中可解析 `start` 值，但当前生成的 `spawn.ini` 未将起点信息写入（若需要保留起点信息需定义输出字段格式并实现）。
- map 文件安装（复制到 game path）：当前仅生成 INI，不会把 map 文件复制到 `--game-path`。
- 单元测试：当前无自动单元测试，仅做了手动 smoke 测试；推荐添加解析器/输出格式的单元测试。
- 错误与验证：当前对 map 内容的健壮性检测有限（假设 map 是有效 INI 格式）；可增强对损坏/异常文件的校验与更友好的错误信息。

Smoke tests（已执行）
---
- 创建临时 map：`Tools/DTA.CLI/test.map`（包含 `[Basic]` 与 `[Map]` 节），运行：

```powershell
dotnet run --project Tools/DTA.CLI -- map:generate-spawnmap "Tools\DTA.CLI\test.map" --out "Tools\DTA.CLI\spawnmap.ini" --dry-run
```

输出示例（`spawnmap.ini` 内容）：

```
[Basic]
OriginalFilename=test.map
Name=TestMap
Author=Copilot

[Map]
Width=100
Height=100
```

- 生成 spawn.ini（dry-run）：

```powershell
dotnet run --project Tools/DTA.CLI -- map:generate-spawnini "Tools\DTA.CLI\test.map" --player "Host,0,1" --player "Bob,1,2" --ai 1 --seed 12345 --out "Tools\DTA.CLI\spawn.ini" --dry-run
```

输出示例（`spawn.ini` 内容）：

```
[Settings]
Name=Host
Scenario=spawnmap.ini
UIMapName=test
PlayerCount=2
Side=0
Color=1
AIPlayers=1
Seed=12345

[Other1]
Name=Bob
Side=1
Color=2
```

参考实现文件
---
- 核心实现：`Tools/DTA.CLI/Program.cs`
- 项目配置：`Tools/DTA.CLI/DTA.CLI.csproj`

建议后续工作（优先级排序）
---
1. （高）将 `UIMapName` 改为优先使用 map `[Basic]` 的 `Name` 字段。
2. （中）实现 supplemental 文件复制（可选 `--copy-supplemental` 开关）。
3. （中）把 `start` 字段写入 spawn.ini（若游戏/客户端支持），或在文档中明确该字段用途。
4. （中）添加单元测试覆盖解析逻辑与输出格式（`--dry-run --format json` 可用于断言）。
5. （低）把常用参数解析替换为成熟的 CLI 库（如 System.CommandLine），改善 help 与错误信息。

附录：已检查的行为匹配清单
---
- [x] 保持 map 原始 INI 内容并插入/更新 `OriginalFilename`。
- [x] 生成 `spawn.ini` 的 `[Settings]` 与 `OtherN` 基本字段。
- [x] 支持 `--out` 与 `--dry-run`。
- [x] 支持重复 `--player`。
- [ ] 未实现 supplemental 文件复制与 map 安装（主动复制）。
