using System;
using Rampastring.Tools;
using Rampastring.XNAUI;
using ClientGUI;
using System.Collections.Generic;
using System.Linq;
using DTAClient.Domain.Multiplayer;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    public enum CheckBoxMapScoringMode
    {
        /// <summary>
        /// 复选框的值对地图评分没有影响。
        /// </summary>
        Irrelevant = 0,

        /// <summary>
        /// 当复选框被选中时阻止地图评分。
        /// </summary>
        DenyWhenChecked = 1,

        /// <summary>
        /// 当复选框未选中时阻止地图评分。
        /// </summary>
        DenyWhenUnchecked = 2
    }

    /// <summary>
    /// 游戏大厅的游戏选项复选框。
    /// </summary>
    public class GameLobbyCheckBox : XNAClientCheckBox
    {
        public GameLobbyCheckBox(WindowManager windowManager) : base (windowManager) { }

        public bool IsMultiplayer { get; set; }

        /// <summary>
        /// 此复选框最后由主机定义的值。
        /// 在复选框初始化后默认为Checked的默认值，
        /// 但其值仅在用户交互时更改。
        /// </summary>
        public bool HostChecked { get; set; }

        /// <summary>
        /// 此复选框最后由本地玩家设定的值。
        /// 在复选框初始化后默认为Checked的默认值，
        /// 但其值仅在用户交互时更改。
        /// </summary>
        public bool UserChecked { get; set; }

        /// <summary>
        /// 此复选框选中时不允许的阵营索引。
        /// 默认为-1，表示无。
        /// </summary>
        public List<int> DisallowedSideIndices = new List<int>();

        public bool AllowChanges { get; set; } = true;

        public CheckBoxMapScoringMode MapScoringMode { get; private set; } = CheckBoxMapScoringMode.Irrelevant;

        private string spawnIniOption;

        private string customIniPath;

        private bool reversed;

        private bool defaultValue;

        private string enabledSpawnIniValue = "True";
        private string disabledSpawnIniValue = "False";

        public List<string> ControlName;

        public List<string> ControlIndex;

        public override void Initialize()
        {
            // 找到此控件所属的游戏大厅并将自己注册为游戏选项。

            XNAControl parent = Parent;
            while (true)
            {
                if (parent == null)
                    break;

                // 哦不，我们这里有一个循环类引用！
                if (parent is GameLobbyBase gameLobby)
                {
                    if (gameLobby.CheckBoxes.Find(chk => chk.Name == this.Name)==null)
                    gameLobby.CheckBoxes.Add(this);
                    break;
                }

                parent = parent.Parent;
            }

            base.Initialize();
        }

        public override void ParseAttributeFromINI(IniFile iniFile, string key, string value)
        {
            switch (key)
            {
                case "SpawnIniOption":
                    spawnIniOption = value;
                    return;
                case "EnabledSpawnIniValue":
                    enabledSpawnIniValue = value;
                    return;
                case "DisabledSpawnIniValue":
                    disabledSpawnIniValue = value;
                    return;
                case "CustomIniPath":
                    customIniPath = value;
                    return;
                case "Reversed":
                    reversed = Conversions.BooleanFromString(value, false);
                    return;
                case "CheckedMP":
                    if (IsMultiplayer)
                        Checked = Conversions.BooleanFromString(value, false);
                    return;
                case "Checked":
                    bool checkedValue = Conversions.BooleanFromString(value, false);
                    Checked = checkedValue;
                    defaultValue = checkedValue;
                    HostChecked = checkedValue;
                    UserChecked = checkedValue;
                    return;
                case "DisallowedSideIndex":
                case "DisallowedSideIndices":
                    List<int> sides = value.Split(',').ToList()
                        .Select(s => Conversions.IntFromString(s, -1)).Distinct().ToList();
                    DisallowedSideIndices.AddRange(sides.Where(s => !DisallowedSideIndices.Contains(s)));
                    return;
                case "MapScoringMode":
                    MapScoringMode = (CheckBoxMapScoringMode)Enum.Parse(typeof(CheckBoxMapScoringMode), value);
                    return;
                case "ControlName":
                    ControlName = value.Split(',').ToList();
                    return;
                case "ControlIndex":
                    ControlIndex = value.Split(',').ToList();
                    return;

            }

            base.ParseAttributeFromINI(iniFile, key, value);
        }

        /// <summary>
        /// 将复选框的关联代码应用到spawn INI文件。
        /// </summary>
        /// <param name="spawnIni">spawn INI文件。</param>
        public void ApplySpawnINICode(IniFile spawnIni)
        {
            if (string.IsNullOrEmpty(spawnIniOption))
                return;

            string value = disabledSpawnIniValue;
            if (Checked != reversed)
            {
                value = enabledSpawnIniValue;
            }

            spawnIni.SetStringValue("Settings", spawnIniOption, value);
        }

        /// <summary>
        /// 将复选框的关联代码应用到地图INI文件。
        /// </summary>
        /// <param name="mapIni">地图INI文件。</param>
        /// <param name="gameMode">当前选中的游戏模式（如果已设置）。</param>
        public void ApplyMapCode(IniFile mapIni, GameMode gameMode)
        {
            if (Checked == reversed || String.IsNullOrEmpty(customIniPath))
                return;

            MapCodeHelper.ApplyMapCode(mapIni, customIniPath, gameMode);
        }

        /// <summary>
        /// 将复选框的不允许阵营索引应用到决定哪些阵营被禁用的布尔数组。
        /// </summary>
        /// <param name="disallowedArray">决定哪些阵营被禁用的数组。</param>
        public void ApplyDisallowedSideIndex(bool[] disallowedArray)
        {
            if (DisallowedSideIndices == null || DisallowedSideIndices.Count == 0)
                return;

            if (Checked != reversed)
            {
                for (int i = 0; i < DisallowedSideIndices.Count; i++)
                {
                    int sideNotAllowed = DisallowedSideIndices[i];
                    disallowedArray[sideNotAllowed] = true;
                }
            }
        }

        public override void OnLeftClick()
        {
            if (!AllowChanges)
                return;

            base.OnLeftClick();
            UserChecked = Checked;

            
        }
    }
}
