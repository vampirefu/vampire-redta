# UI INI 业务逻辑配置项参考文档

本文档梳理了客户端选项系统中具有**实际业务逻辑**的INI配置项，包括文件操作、INI设置读写、父子联动等机制。

---

## 一、选项控件体系概览

选项控件通过 `OptionsWindow.ini` 中的 `[xxxExtraControls]` 段动态注册到各选项面板中。支持以下控件类型：

| 控件类型 | 源码类 | 业务逻辑 |
|----------|--------|----------|
| `FileSettingCheckBox` | `DTAConfig.Settings.FileSettingCheckBox` | 勾选/取消时执行文件复制/删除操作，同时写入INI设置 |
| `SettingCheckBox` | `DTAConfig.Settings.SettingCheckBox` | 勾选/取消时仅写入INI设置（支持自定义启用/禁用值） |
| `FileSettingDropDown` | `DTAConfig.Settings.FileSettingDropDown` | 切换选项时执行文件复制/删除操作，同时写入INI设置 |
| `SettingDropDown` | `DTAConfig.Settings.SettingDropDown` | 切换选项时仅写入INI设置（支持写入项文本值） |

### 注册方式

在 `[面板名ExtraControls]` 段中，以 `序号=控件名:控件类型` 格式注册：

```ini
[DisplayOptionsPanelExtraControls]
0=chkMEDDraw:FileSettingCheckBox

[GameOptionsPanelExtraControls]
0=chkTooltipsExtra:SettingCheckBox
```

---

## 二、FileSettingCheckBox — 文件操作复选框

### 2.1 专属配置键

| 配置键 | 格式 | 说明 |
|--------|------|------|
| `EnabledFileN` | `源路径,目标路径,文件操作选项` | 勾选时应用的文件操作列表（N从0递增） |
| `DisabledFileN` | `源路径,目标路径,文件操作选项` | 取消勾选时应用的文件操作列表（N从0递增） |
| `CheckAvailability` | `true/false` | 是否检查源文件可用性来决定控件是否可选 |
| `ResetUnavailableValue` | `true/false` | 当所选状态不可用时是否自动重置为默认值 |
| `Reversed` | `true/false` | （旧版兼容）是否反转文件存在性与勾选状态的映射 |

### 2.2 文件操作选项（FileOperationOptions）

| 选项值 | Apply 行为 | Revert 行为 |
|--------|-----------|-------------|
| `AlwaysOverwrite` | 始终从源路径覆盖复制到目标路径 | 删除目标路径文件 |
| `OverwriteOnMismatch` | 比较SHA1，不一致时才覆盖复制 | 删除目标路径文件 |
| `DontOverwrite` | 仅在目标文件不存在时复制 | 删除目标路径文件 |
| `KeepChanges` | 首次从源复制，后续保留用户修改（缓存到SettingsCache） | 缓存用户修改后删除目标文件 |

### 2.3 保存行为

- 勾选时：先 Revert 所有 `DisabledFile`，再 Apply 所有 `EnabledFile`，写入 `SettingSection/SettingKey=true`
- 取消勾选时：先 Revert 所有 `EnabledFile`，再 Apply 所有 `DisabledFile`，写入 `SettingSection/SettingKey=false`
- 文件路径均相对于游戏根目录 (`ProgramConstants.GamePath`)

---

## 三、SettingCheckBox — INI设置复选框

### 3.1 专属配置键

| 配置键 | 格式 | 说明 |
|--------|------|------|
| `WriteSettingValue` | `true/false` | 是否使用自定义启用/禁用值（替代布尔值） |
| `EnabledSettingValue` | 字符串 | 勾选时写入INI的值（需 `WriteSettingValue=true`） |
| `DisabledSettingValue` | 字符串 | 取消勾选时写入INI的值（需 `WriteSettingValue=true`） |

### 3.2 保存行为

- `WriteSettingValue=false`（默认）：直接写入 `true/false` 到 `SettingSection/SettingKey`
- `WriteSettingValue=true`：写入 `EnabledSettingValue/DisabledSettingValue` 到 `SettingSection/SettingKey`

---

## 四、FileSettingDropDown — 文件操作下拉框

### 4.1 专属配置键

| 配置键 | 格式 | 说明 |
|--------|------|------|
| `ItemNFile` | `源路径,目标路径,文件操作选项` | 选中第N项时应用的文件操作列表（N从0递增） |
| `CheckAvailability` | `true/false` | 是否检查源文件可用性来决定选项是否可选 |
| `ResetUnavailableValue` | `true/false` | 当所选项不可用时是否自动重置为默认值 |

### 4.2 保存行为

- 保存时：先 Revert 所有未选项的文件，再 Apply 所选项的文件
- 写入 `SelectedIndex` 到 `SettingSection/SettingKey`

---

## 五、SettingDropDown — INI设置下拉框

### 5.1 专属配置键

| 配置键 | 格式 | 说明 |
|--------|------|------|
| `Items` | `项1,项2,项3,...` | 下拉框选项列表 |
| `WriteItemValue` | `true/false` | 是否写入选项文本而非索引 |

### 5.2 保存行为

- `WriteItemValue=false`（默认）：写入 `SelectedIndex` 到 `SettingSection/SettingKey`
- `WriteItemValue=true`：写入 `SelectedItem.Text` 到 `SettingSection/SettingKey`

---

## 六、通用配置键（所有选项控件共享）

以下配置键适用于所有四种选项控件：

| 配置键 | 格式 | 说明 |
|--------|------|------|
| `DefaultValue` | 布尔值/整数 | 控件默认值 |
| `SettingSection` | 字符串 | 写入用户INI的节名（默认 `CustomSettings`） |
| `SettingKey` | 字符串 | 写入用户INI的键名（默认 `控件名_Checked` 或 `控件名_SelectedIndex`） |
| `RestartRequired` | `true/false` | 更改此项是否需要重启客户端 |
| `ParentCheckBoxName` | 字符串 | 父复选框控件名（仅 CheckBox） |
| `ParentCheckBoxRequiredValue` | `true/false` | 父复选框要求的值（仅 CheckBox，默认 `true`） |
| `Location` | `X,Y` | 控件位置 |
| `Text` | 字符串 | 控件显示文本 |
| `ToolTip` | 字符串 | 提示文本（`@` 分隔多行） |

---

## 七、当前游戏环境中的业务逻辑配置项实例

> 来源：`AGWar1.3.1\Resources\OptionsWindow.ini`

### 7.1 chkMEDDraw（FileSettingCheckBox）

| 属性 | 值 |
|------|-----|
| 控件类型 | `FileSettingCheckBox` |
| 所属面板 | DisplayOptionsPanel |
| 位置 | 285,114 |
| 文本 | Enable DDWrapper for map editor |
| 提示 | Enables DirectDraw wrapper & emulation for map editor. Turning this option on can help if you are encountering problems with editor viewport not displaying or being laggy. |
| 默认值 | false |
| INI节/键 | Video / UseDDWrapperForMapEditor |

**文件操作：**

| 条件 | 操作 | 源文件 | 目标文件 | 选项 |
|------|------|--------|----------|------|
| 勾选 | EnabledFile0 | `Resources/Compatibility/DLL/ddwrapper.dll` | `FinalAlert2SP/ddraw32.dll` | OverwriteOnMismatch |
| 勾选 | EnabledFile1 | `Resources/Compatibility/Configs/aqrit.cfg` | `FinalAlert2SP/aqrit.cfg` | KeepChanges |

**业务逻辑：** 为地图编辑器（FinalAlert2SP）启用 DDWrapper (ddraw32.dll) 和对应配置文件 (aqrit.cfg)，解决编辑器视口不显示或卡顿的问题。

---

### 7.2 chkTooltipsExtra（SettingCheckBox，已注释）

| 属性 | 值 |
|------|-----|
| 控件类型 | `SettingCheckBox` |
| 所属面板 | GameOptionsPanel |
| 位置 | 24,151 |
| 文本 | Sidebar Tooltip Descriptions |
| 默认值 | true |
| INI节/键 | Phobos / ToolTipDescriptions |
| 父控件 | chkTooltips（要求值为 true） |

**业务逻辑：** 在侧边栏工具提示中启用额外描述信息。依赖 Phobos 引擎，需父控件 chkTooltips 勾选时才可用。

---

### 7.3 chkPrioritySelection（SettingCheckBox，已注释）

| 属性 | 值 |
|------|-----|
| 控件类型 | `SettingCheckBox` |
| 所属面板 | GameOptionsPanel |
| 位置 | 242,54 |
| 文本 | Mass Selection Filtering |
| 默认值 | false |
| INI节/键 | Phobos / PrioritySelectionFiltering |

**业务逻辑：** 启用批量选择过滤——框选时非战斗单位不会与战斗单位一起被选中。依赖 Phobos 引擎。

---

### 7.4 chkBuildingPlacement（SettingCheckBox，已注释）

| 属性 | 值 |
|------|-----|
| 控件类型 | `SettingCheckBox` |
| 所属面板 | GameOptionsPanel |
| 位置 | 242,78 |
| 文本 | Show Building Placement Preview |
| 默认值 | false |
| INI节/键 | Phobos / ShowBuildingPlacementPreview |

**业务逻辑：** 放置建筑时显示建筑预览图像。依赖 Phobos 引擎。

---

## 八、旧版兼容：File 键（已弃用）

`FileSettingCheckBox` 支持旧版 `FileN` 键格式（不带 Enabled/Disabled 前缀），使用此格式时进入旧版兼容模式：

- 仅支持 `File0`, `File1`, ... 键
- 行为：勾选时 Apply 所有文件，取消时 Revert 所有文件
- 初始状态通过检查目标文件是否存在来决定
- 可配合 `Reversed=true` 反转逻辑

> 当前游戏环境中未使用旧版格式。

---

## 九、源码文件索引

| 文件 | 说明 |
|------|------|
| `DTAConfig/Settings/FileSettingCheckBox.cs` | 文件操作复选框实现 |
| `DTAConfig/Settings/SettingCheckBox.cs` | INI设置复选框实现 |
| `DTAConfig/Settings/FileSettingDropDown.cs` | 文件操作下拉框实现 |
| `DTAConfig/Settings/SettingDropDown.cs` | INI设置下拉框实现 |
| `DTAConfig/Settings/SettingCheckBoxBase.cs` | 复选框基类（通用属性、父子联动） |
| `DTAConfig/Settings/SettingDropDownBase.cs` | 下拉框基类（通用属性、Items解析） |
| `DTAConfig/Settings/FileSourceDestinationInfo.cs` | 文件源/目标/操作选项解析与执行 |
| `DTAConfig/OptionPanels/XNAOptionsPanel.cs` | 选项面板基类（ExtraControls加载、设置保存） |
| `ClientGUI/XNAWindowBase.cs` | ParseExtraControls 实现 |
| `DTAConfig/OptionsWindow.cs` | 选项窗口主逻辑 |
