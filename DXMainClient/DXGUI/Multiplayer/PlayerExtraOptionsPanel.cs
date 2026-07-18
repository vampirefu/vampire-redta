using System;
using System.Collections.Generic;
using System.Linq;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer
{
    public class PlayerExtraOptionsPanel : XNAWindow
    {
        private const int maxStartCount = 8;
        private const int defaultX = 24;
        private const int defaultTeamStartMappingX = UIDesignConstants.EMPTY_SPACE_SIDES;
        private const int teamMappingPanelWidth = 50;
        private const int teamMappingPanelHeight = 22;
        private readonly string customPresetName = "自定义";

        private XNAClientCheckBox chkBoxForceRandomSides;
        private XNAClientCheckBox chkBoxForceRandomTeams;
        private XNAClientCheckBox chkBoxForceRandomColors;
        private XNAClientCheckBox chkBoxForceRandomStarts;
        private XNAClientCheckBox chkBoxUseTeamStartMappings;
        private XNAClientDropDown ddTeamStartMappingPreset;
        private TeamStartMappingsPanel teamStartMappingsPanel;
        private bool _isHost;
        private bool ignoreMappingChanges;

        public EventHandler OptionsChanged;
        public EventHandler OnClose;

        private Map _map;

        public PlayerExtraOptionsPanel(WindowManager windowManager) : base(windowManager)
        {
        }

        public bool IsForcedRandomSides() => chkBoxForceRandomSides.Checked;
        public bool IsForcedRandomTeams() => chkBoxForceRandomTeams.Checked;
        public bool IsForcedRandomColors() => chkBoxForceRandomColors.Checked;
        public bool IsForcedRandomStarts() => chkBoxForceRandomStarts.Checked;
        public bool IsUseTeamStartMappings() => chkBoxUseTeamStartMappings.Checked;

        private void Options_Changed(object sender, EventArgs e) => OptionsChanged?.Invoke(sender, e);

        private void Mapping_Changed(object sender, EventArgs e)
        {
            Options_Changed(sender, e);
            if (ignoreMappingChanges)
                return;

            ddTeamStartMappingPreset.SelectedIndex = 0;
        }

        private void ChkBoxUseTeamStartMappings_Changed(object sender, EventArgs e)
        {
            RefreshTeamStartMappingsPanel();
            chkBoxForceRandomTeams.Checked = chkBoxForceRandomTeams.Checked || chkBoxUseTeamStartMappings.Checked;
            chkBoxForceRandomTeams.AllowChecking = !chkBoxUseTeamStartMappings.Checked;

            // chkBoxForceRandomStarts.Checked = chkBoxForceRandomStarts.Checked || chkBoxUseTeamStartMappings.Checked;
            // chkBoxForceRandomStarts.AllowChecking = !chkBoxUseTeamStartMappings.Checked;

            RefreshPresetDropdown();

            Options_Changed(sender, e);
        }

        private void RefreshTeamStartMappingsPanel()
        {
            teamStartMappingsPanel.EnableControls(_isHost && chkBoxUseTeamStartMappings.Checked);

            RefreshTeamStartMappingPanels();
        }

        private void AddLocationAssignments()
        {
            for (int i = 0; i < maxStartCount; i++)
            {
                var teamStartMappingPanel = new TeamStartMappingPanel(WindowManager, i + 1);
                teamStartMappingPanel.ClientRectangle = GetTeamMappingPanelRectangle(i);

                teamStartMappingsPanel.AddMappingPanel(teamStartMappingPanel);
            }

            teamStartMappingsPanel.MappingChanged += Mapping_Changed;
        }

        private Rectangle GetTeamMappingPanelRectangle(int index)
        {
            const int maxColumnCount = 2;
            const int mappingPanelDefaultX = 4;
            const int mappingPanelDefaultY = 0;
            if (index > 0 && index % maxColumnCount == 0) // 需要开始新列
                return new Rectangle(((index / maxColumnCount) * (teamMappingPanelWidth + mappingPanelDefaultX)) + 3, mappingPanelDefaultY, teamMappingPanelWidth, teamMappingPanelHeight);

            var lastControl = index > 0 ? teamStartMappingsPanel.GetTeamStartMappingPanels()[index - 1] : null;
            return new Rectangle(lastControl?.X ?? mappingPanelDefaultX, lastControl?.Bottom + 4 ?? mappingPanelDefaultY, teamMappingPanelWidth, teamMappingPanelHeight);
        }

        private void ClearTeamStartMappingSelections()
            => teamStartMappingsPanel.GetTeamStartMappingPanels().ForEach(panel => panel.ClearSelections());

        private void RefreshTeamStartMappingPanels()
        {
            ClearTeamStartMappingSelections();
            var teamStartMappingPanels = teamStartMappingsPanel.GetTeamStartMappingPanels();
            for (int i = 0; i < teamStartMappingPanels.Count; i++)
            {
                var teamStartMappingPanel = teamStartMappingPanels[i];
                teamStartMappingPanel.ClearSelections();
                if (!IsUseTeamStartMappings())
                    continue;

                teamStartMappingPanel.EnableControls(_isHost && chkBoxUseTeamStartMappings.Checked && i < _map?.MaxPlayers);
                RefreshTeamStartMappingPresets(_map?.TeamStartMappingPresets);
            }
        }

        private void RefreshTeamStartMappingPresets(List<TeamStartMappingPreset> teamStartMappingPresets)
        {
            ddTeamStartMappingPreset.Items.Clear();
            ddTeamStartMappingPreset.AddItem(new XNADropDownItem
            {
                Text = customPresetName,
                Tag = new List<TeamStartMapping>()
            });
            ddTeamStartMappingPreset.SelectedIndex = 0;

            if (!(teamStartMappingPresets?.Any() ?? false)) return;

            teamStartMappingPresets.ForEach(preset => ddTeamStartMappingPreset.AddItem(new XNADropDownItem
            {
                Text = preset.Name,
                Tag = preset.TeamStartMappings
            }));
            ddTeamStartMappingPreset.SelectedIndex = 1;
        }

        private void DdTeamMappingPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = ddTeamStartMappingPreset.SelectedItem;
            if (selectedItem?.Text == customPresetName)
                return;

            var teamStartMappings = selectedItem?.Tag as List<TeamStartMapping>;

            ignoreMappingChanges = true;
            teamStartMappingsPanel.SetTeamStartMappings(teamStartMappings);
            ignoreMappingChanges = false;
        }

        private void RefreshPresetDropdown() => ddTeamStartMappingPreset.AllowDropDown = _isHost && chkBoxUseTeamStartMappings.Checked;

        public override void Initialize()
        {
            Name = nameof(PlayerExtraOptionsPanel);
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 255), 1, 1);
            Visible = false;

            var btnClose = new XNAClientButton(WindowManager);
            btnClose.Name = nameof(btnClose);
            btnClose.ClientRectangle = new Rectangle(0, 0, 0, 0);
            btnClose.IdleTexture = AssetLoader.LoadTexture("optionsButtonClose.png");
            btnClose.HoverTexture = AssetLoader.LoadTexture("optionsButtonClose_c.png");
            btnClose.LeftClick += (sender, args) => Disable();
            AddChild(btnClose);

            var lblHeader = new XNALabel(WindowManager);
            lblHeader.Name = nameof(lblHeader);
            lblHeader.Text = "额外玩家选项";
            lblHeader.ClientRectangle = new Rectangle(defaultX, 4, 0, 18);
            AddChild(lblHeader);

            chkBoxForceRandomSides = new XNAClientCheckBox(WindowManager);
            chkBoxForceRandomSides.Name = nameof(chkBoxForceRandomSides);
            chkBoxForceRandomSides.Text = "强制随机阵营";
            chkBoxForceRandomSides.ClientRectangle = new Rectangle(defaultX, lblHeader.Bottom + 4, 0, 0);
            chkBoxForceRandomSides.CheckedChanged += Options_Changed;
            AddChild(chkBoxForceRandomSides);

            chkBoxForceRandomColors = new XNAClientCheckBox(WindowManager);
            chkBoxForceRandomColors.Name = nameof(chkBoxForceRandomColors);
            chkBoxForceRandomColors.Text = "强制随机颜色";
            chkBoxForceRandomColors.ClientRectangle = new Rectangle(defaultX, chkBoxForceRandomSides.Bottom + 4, 0, 0);
            chkBoxForceRandomColors.CheckedChanged += Options_Changed;
            AddChild(chkBoxForceRandomColors);

            chkBoxForceRandomTeams = new XNAClientCheckBox(WindowManager);
            chkBoxForceRandomTeams.Name = nameof(chkBoxForceRandomTeams);
            chkBoxForceRandomTeams.Text = "强制随机队伍";
            chkBoxForceRandomTeams.ClientRectangle = new Rectangle(defaultX, chkBoxForceRandomColors.Bottom + 4, 0, 0);
            chkBoxForceRandomTeams.CheckedChanged += Options_Changed;
            AddChild(chkBoxForceRandomTeams);

            chkBoxForceRandomStarts = new XNAClientCheckBox(WindowManager);
            chkBoxForceRandomStarts.Name = nameof(chkBoxForceRandomStarts);
            chkBoxForceRandomStarts.Text = "强制随机起始位置";
            chkBoxForceRandomStarts.ClientRectangle = new Rectangle(defaultX, chkBoxForceRandomTeams.Bottom + 4, 0, 0);
            chkBoxForceRandomStarts.CheckedChanged += Options_Changed;
            AddChild(chkBoxForceRandomStarts);

            /////////////////////////////

            chkBoxUseTeamStartMappings = new XNAClientCheckBox(WindowManager);
            chkBoxUseTeamStartMappings.Name = nameof(chkBoxUseTeamStartMappings);
            chkBoxUseTeamStartMappings.Text = "启用自动结盟:";
            chkBoxUseTeamStartMappings.ClientRectangle = new Rectangle(chkBoxForceRandomSides.X, chkBoxForceRandomStarts.Bottom + 20, 0, 0);
            chkBoxUseTeamStartMappings.CheckedChanged += ChkBoxUseTeamStartMappings_Changed;
            AddChild(chkBoxUseTeamStartMappings);

            var btnHelp = new XNAClientButton(WindowManager);
            btnHelp.Name = nameof(btnHelp);
            btnHelp.IdleTexture = AssetLoader.LoadTexture("questionMark.png");
            btnHelp.HoverTexture = AssetLoader.LoadTexture("questionMark_c.png");
            btnHelp.LeftClick += BtnHelp_LeftClick;
            btnHelp.ClientRectangle = new Rectangle(chkBoxUseTeamStartMappings.Right + 4, chkBoxUseTeamStartMappings.Y - 1, 0, 0);
            AddChild(btnHelp);

            var lblPreset = new XNALabel(WindowManager);
            lblPreset.Name = nameof(lblPreset);
            lblPreset.Text = "预设:";
            lblPreset.ClientRectangle = new Rectangle(chkBoxUseTeamStartMappings.X, chkBoxUseTeamStartMappings.Bottom + 8, 0, 0);
            AddChild(lblPreset);

            ddTeamStartMappingPreset = new XNAClientDropDown(WindowManager);
            ddTeamStartMappingPreset.Name = nameof(ddTeamStartMappingPreset);
            ddTeamStartMappingPreset.ClientRectangle = new Rectangle(lblPreset.X + 50, lblPreset.Y - 2, 160, 0);
            ddTeamStartMappingPreset.SelectedIndexChanged += DdTeamMappingPreset_SelectedIndexChanged;
            ddTeamStartMappingPreset.AllowDropDown = true;
            AddChild(ddTeamStartMappingPreset);

            teamStartMappingsPanel = new TeamStartMappingsPanel(WindowManager);
            teamStartMappingsPanel.Name = nameof(teamStartMappingsPanel);
            teamStartMappingsPanel.ClientRectangle = new Rectangle(lblPreset.X, ddTeamStartMappingPreset.Bottom + 8, Width, Height - ddTeamStartMappingPreset.Bottom + 4);
            AddChild(teamStartMappingsPanel);

            AddLocationAssignments();

            base.Initialize();

            RefreshTeamStartMappingsPanel();
        }

        private void BtnHelp_LeftClick(object sender, EventArgs args)
        {
            XNAMessageBox.Show(WindowManager, "自动结盟",
                ("自动结盟功能允许游戏主持将位置指定小队,而不是指定玩家。\n" +
                "当玩家选择位置后,他将自动在地图上指定小队。\n" +
                "这对于随机位置或随机小队都很好方便,然而只需随机小队。\n" +
                "手动选择位置更优先。\n\n") +
                $"{TeamStartMapping.NO_TEAM} : " + "禁止此位置指定玩家" + "\n" +
                $"{TeamStartMapping.RANDOM_TEAM} : "+"允许玩家在此位置,但不能指定小队"
            );
        }

        public void UpdateForMap(Map map)
        {
            if (_map == map)
                return;

            _map = map;

            RefreshTeamStartMappingPanels();
        }

        public List<TeamStartMapping> GetTeamStartMappings()
            => chkBoxUseTeamStartMappings.Checked ?
                teamStartMappingsPanel.GetTeamStartMappings() : new List<TeamStartMapping>();

        public void EnableControls(bool enable)
        {
            chkBoxForceRandomSides.InputEnabled = enable;
            chkBoxForceRandomColors.InputEnabled = enable;
            chkBoxForceRandomStarts.InputEnabled = enable;
            chkBoxForceRandomTeams.InputEnabled = enable;
            chkBoxUseTeamStartMappings.InputEnabled = enable;

            teamStartMappingsPanel.EnableControls(enable && chkBoxUseTeamStartMappings.Checked);
        }

        public PlayerExtraOptions GetPlayerExtraOptions()
            => new PlayerExtraOptions()
            {
                IsForceRandomSides = IsForcedRandomSides(),
                IsForceRandomColors = IsForcedRandomColors(),
                IsForceRandomStarts = IsForcedRandomStarts(),
                IsForceRandomTeams = IsForcedRandomTeams(),
                IsUseTeamStartMappings = IsUseTeamStartMappings(),
                TeamStartMappings = GetTeamStartMappings()
            };

        public void SetPlayerExtraOptions(PlayerExtraOptions playerExtraOptions)
        {
            chkBoxForceRandomSides.Checked = playerExtraOptions.IsForceRandomSides;
            chkBoxForceRandomColors.Checked = playerExtraOptions.IsForceRandomColors;
            chkBoxForceRandomTeams.Checked = playerExtraOptions.IsForceRandomTeams;
            chkBoxForceRandomStarts.Checked = playerExtraOptions.IsForceRandomStarts;
            chkBoxUseTeamStartMappings.Checked = playerExtraOptions.IsUseTeamStartMappings;
            teamStartMappingsPanel.SetTeamStartMappings(playerExtraOptions.TeamStartMappings);
        }

        public void SetIsHost(bool isHost)
        {
            _isHost = isHost;
            RefreshPresetDropdown();
            EnableControls(_isHost);
        }
    }
}
