# DTA CLI 能力清单（精简版）

本文件明确限定当前 `Tools/DTA.CLI` 的单一职责：从提供的地图文件生成游戏所需的两份 INI 文件 — `spawnmap.ini` 和 `spawn.ini`。其他功能（渲染、随机地图生成、地图列表、预览提取等）将拆解到独立工具/CLI 中。

## 核心职责
- `map:generate-spawnmap <mapFile> --game-path <dir> [--out <file>] [--dry-run]`
  - 作用：读取指定 `.map`（或其对应 map.ini），并生成 `spawnmap.ini`。实现要求：保留 map 的原始 INI 内容，确保 `[Basic]` 段包含 `OriginalFilename=<mapFileName>`（若不存在则插入）。输出到 `<game-path>/spawnmap.ini` 或 `--out` 指定路径；`--dry-run` 时把结果打印到 stdout。

- `map:generate-spawnini <mapFile> --game-path <dir> --player "name,side,color[,start]" [--player ...] [--ai <n>] [--seed <n>] [--out <file>] [--dry-run]`
  - 作用：基于传入的玩家与 AI 参数生成 `spawn.ini`。行为：写入 `[Settings]`（含 `Scenario=spawnmap.ini`、`UIMapName`、`PlayerCount`、主机 `Name`/`Side`/`Color`、`AIPlayers`、`Seed` 等），并为其他玩家生成 `OtherN` 段（`Name`、`Side`、`Color`）。默认写入 `<game-path>/spawn.ini` 或 `--out` 指定路径；`--dry-run` 时把结果打印到 stdout。

## 参数约定与实现选择
- 玩家参数采用重复 `--player "name,side,color[,start]"` 形式（例如 `--player "Host,0,1" --player "Bob,1,2"`）。
- 默认输出位置为 `--game-path`；同时支持 `--out` 覆盖目标文件路径。
- 初期实现采用纯文本/INI quick-path（读取 map 文件文本、插入/替换字段并写回），以避免引入大型客户端依赖并降低构建/锁文件风险。若将来需要与客户端行为 100% 对齐，可把 `ApplySpawnIniCode()` 的合并逻辑抽取到 `MapGenerator.Core` 并在 CLI 中调用。

## 限制与注意事项
- 本 CLI 当前不承担地图渲染、预览提取或随机地图生成等功能；这些会另建独立工具。
- Supplemental 文件复制（例如地图附带的资源）如需支持，应参考 `ClientCore/ClientConfiguration.cs` 中的 `SupplementalMapFileExtensions` 配置。
- 若在构建或运行 `Tools/DTA.CLI` 时遇到 `Access denied`（DLL 写入被拒绝）或重复 assembly attribute（CS0579）错误，请先释放占用进程或使用项目属性 `GenerateAssemblyInfo=false`，然后重试构建。

## 示例

生成 `spawnmap.ini`（写入游戏路径）：
```bash
dotnet run --project Tools/DTA.CLI -- map:generate-spawnmap "DXMainClient/Resources/Maps/example.map" --game-path "C:\\Games\\YR"
```

生成 `spawn.ini`（dry-run）：
```bash
dotnet run --project Tools/DTA.CLI -- map:generate-spawnini "path/to/map.map" --player "Host,0,1" --player "Bob,1,2" --ai 1 --seed 12345 --dry-run
```

---

作者：开发助理
日期：2026-05-04
