DTA.CLI

Usage examples:

Build and run:

```bash
cd Tools/DTA.CLI
dotnet build
dotnet run -- file:sha1 "path/to/file"
dotnet run -- map:extract-preview "path/to/map.map" --out preview.png
dotnet run -- map:generate-spawnmap "path/to/map.map" --game-path "C:\\Games\\YR"
dotnet run -- map:generate-spawnini "path/to/map.map" --game-path "C:\\Games\\YR" --player "Host,0,1" --player "Bob,1,2" --ai 1 --seed 12345
```

Notes:
- The project references `MapGenerator.Core` for preview extraction; ensure the solution restores packages.
- `file:sha1` uses `Utilities.CalculateSHA1ForFile` from the repo utilities.
