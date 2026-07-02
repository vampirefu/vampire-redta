using System;
using System.Linq;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using DTAClient.Online.EventArguments;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class LoadOrSaveGameOptionPresetWindow : XNAWindow
    {
        private bool _isLoad;

        private readonly XNALabel lblHeader;

        private readonly XNADropDownItem ddiCreatePresetItem;

        private readonly XNADropDownItem ddiSelectPresetItem;

        private readonly XNAClientButton btnLoadSave;

        private readonly XNAClientButton btnDelete;

        private readonly XNAClientDropDown ddPresetSelect;

        private readonly XNALabel lblNewPresetName;

        private readonly XNATextBox tbNewPresetName;

        public EventHandler<GameOptionPresetEventArgs> PresetLoaded;

        public EventHandler<GameOptionPresetEventArgs> PresetSaved;

        public LoadOrSaveGameOptionPresetWindow(WindowManager windowManager) : base(windowManager)
        {
            ClientRectangle = new Rectangle(0, 0, 325, 185);

            var margin = 10;

            lblHeader = new XNALabel(WindowManager);
            lblHeader.Name = nameof(lblHeader);
            lblHeader.FontIndex = 1;
            lblHeader.ClientRectangle = new Rectangle(
                margin, margin,
                150, 22
            );

            var lblPresetName = new XNALabel(WindowManager);
            lblPresetName.Name = nameof(lblPresetName);
            lblPresetName.Text = "预设名称";
            lblPresetName.ClientRectangle = new Rectangle(
                margin, lblHeader.Bottom + margin,
                150, 18
            );

            ddiCreatePresetItem = new XNADropDownItem();
            ddiCreatePresetItem.Text = "[创建新预设]";

            ddiSelectPresetItem = new XNADropDownItem();
            ddiSelectPresetItem.Text = "[选择预设]";
            ddiSelectPresetItem.Selectable = false;

            ddPresetSelect = new XNAClientDropDown(WindowManager);
            ddPresetSelect.Name = nameof(ddPresetSelect);
            ddPresetSelect.ClientRectangle = new Rectangle(
                10, lblPresetName.Bottom + 2,
                150, 22
            );
            ddPresetSelect.SelectedIndexChanged += DropDownPresetSelect_SelectedIndexChanged;

            lblNewPresetName = new XNALabel(WindowManager);
            lblNewPresetName.Name = nameof(lblNewPresetName);
            lblNewPresetName.Text = "新预设名称";
            lblNewPresetName.ClientRectangle = new Rectangle(
                margin, ddPresetSelect.Bottom + margin,
                150, 18
            );

            tbNewPresetName = new XNATextBox(WindowManager);
            tbNewPresetName.Name = nameof(tbNewPresetName);
            tbNewPresetName.ClientRectangle = new Rectangle(
                10, lblNewPresetName.Bottom + 2,
                150, 22
            );
            tbNewPresetName.TextChanged += (sender, args) => RefreshButtons();

            btnLoadSave = new XNAClientButton(WindowManager);
            btnLoadSave.Name = nameof(btnLoadSave);
            btnLoadSave.LeftClick += BtnLoadSave_LeftClick;
            btnLoadSave.ClientRectangle = new Rectangle(
                margin,
                Height - UIDesignConstants.BUTTON_HEIGHT - margin,
                UIDesignConstants.BUTTON_WIDTH_92,
                UIDesignConstants.BUTTON_HEIGHT
            );

            btnDelete = new XNAClientButton(WindowManager);
            btnDelete.Name = nameof(btnDelete);
            btnDelete.Text = "删除";
            btnDelete.LeftClick += BtnDelete_LeftClick;
            btnDelete.ClientRectangle = new Rectangle(
                btnLoadSave.Right + margin,
                btnLoadSave.Y,
                UIDesignConstants.BUTTON_WIDTH_92,
                UIDesignConstants.BUTTON_HEIGHT
            );

            var btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Text = "取消";
            btnCancel.ClientRectangle = new Rectangle(
                btnDelete.Right + margin,
                btnLoadSave.Y,
                UIDesignConstants.BUTTON_WIDTH_92,
                UIDesignConstants.BUTTON_HEIGHT
            );
            btnCancel.LeftClick += (sender, args) => Disable();

            AddChild(lblHeader);
            AddChild(lblPresetName);
            AddChild(ddPresetSelect);
            AddChild(lblNewPresetName);
            AddChild(tbNewPresetName);
            AddChild(btnLoadSave);
            AddChild(btnDelete);
            AddChild(btnCancel);

            Disable();
        }

        public override void Initialize()
        {
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 255), 1, 1);
            
            base.Initialize();
        }

        /// <summary>
        /// 显示窗口。
        /// </summary>
        /// <param name="isLoad">窗口的"模式"：加载还是保存。</param>
        public void Show(bool isLoad)
        {
            _isLoad = isLoad;
            lblHeader.Text = _isLoad ? "加载预设" : "保存预设";
            btnLoadSave.Text = _isLoad ? "加载" : "保存";

            if (_isLoad)
                ShowLoad();
            else
                ShowSave();

            RefreshButtons();
            CenterOnParent();
            Enable();
        }

        /// <summary>
        /// 当预设下拉选择更改时的回调
        /// </summary>
        private void DropDownPresetSelect_SelectedIndexChanged(object sender, EventArgs eventArgs)
        {
            if (!_isLoad)
                DropDownPresetSelect_SelectedIndexChanged_IsSave();

            RefreshButtons();
        }

        /// <summary>
        /// 当"保存"模式下预设下拉选择更改时的回调
        /// </summary>
        private void DropDownPresetSelect_SelectedIndexChanged_IsSave()
        {
            if (IsCreatePresetSelected)
            {
                // 当在下拉菜单中选择了"创建"选项时，显示指定新名称的字段
                tbNewPresetName.Enable();
                lblNewPresetName.Enable();
            }
            else
            {
                // 当选择了现有预设时，隐藏指定新名称的字段
                tbNewPresetName.Disable();
                lblNewPresetName.Disable();
            }
        }

        /// <summary>
        /// 刷新加载/保存按钮的状态
        /// </summary>
        private void RefreshButtons()
        {
            if (_isLoad)
                btnLoadSave.Enabled = !IsSelectPresetSelected;
            else
                btnLoadSave.Enabled = !IsCreatePresetSelected || !IsNewPresetNameFieldEmpty;

            btnDelete.Enabled = !IsCreatePresetSelected && !IsSelectPresetSelected;
        }

        private bool IsCreatePresetSelected => ddPresetSelect.SelectedItem == ddiCreatePresetItem;
        private bool IsSelectPresetSelected => ddPresetSelect.SelectedItem == ddiSelectPresetItem;
        private bool IsNewPresetNameFieldEmpty => string.IsNullOrWhiteSpace(tbNewPresetName.Text);

        /// <summary>
        /// 从已保存的预设中填充预设下拉列表
        /// </summary>
        private void LoadPresets()
        {
            ddPresetSelect.Items.Clear();
            ddPresetSelect.Items.Add(_isLoad ? ddiSelectPresetItem : ddiCreatePresetItem);
            ddPresetSelect.SelectedIndex = 0;

            ddPresetSelect.Items.AddRange(GameOptionPresets.Instance
                .GetPresetNames()
                .OrderBy(name => name)
                .Select(name => new XNADropDownItem()
                {
                    Text = name
                }));
        }

        /// <summary>
        /// 在"加载"模式上下文中显示当前窗口
        /// </summary>
        private void ShowLoad()
        {
            LoadPresets();

            // 在"加载"模式下不显示指定预设名称的字段
            lblNewPresetName.Disable();
            tbNewPresetName.Disable();
        }

        /// <summary>
        /// 在"保存"模式上下文中显示当前窗口
        /// </summary>
        private void ShowSave()
        {
            LoadPresets();

            // 在"保存"模式下显示指定预设名称的字段
            lblNewPresetName.Enable();
            tbNewPresetName.Enable();
            tbNewPresetName.Text = string.Empty;
        }

        private void BtnLoadSave_LeftClick(object sender, EventArgs e)
        {
            var selectedItem = ddPresetSelect.Items[ddPresetSelect.SelectedIndex];
            if (_isLoad)
            {
                PresetLoaded?.Invoke(this, new GameOptionPresetEventArgs(selectedItem.Text));
            }
            else
            {
                var presetName = IsCreatePresetSelected ? tbNewPresetName.Text : selectedItem.Text;
                PresetSaved?.Invoke(this, new GameOptionPresetEventArgs(presetName));
            }

            Disable();
        }

        private void BtnDelete_LeftClick(object sender, EventArgs e)
        {
            var selectedItem = ddPresetSelect.Items[ddPresetSelect.SelectedIndex];
            var messageBox = XNAMessageBox.ShowYesNoDialog(WindowManager,
                "预设删除确认",
                "您确定要删除这条预设吗?" + "\n\n" + selectedItem.Text);
            messageBox.YesClickedAction = box =>
            {
                GameOptionPresets.Instance.DeletePreset(selectedItem.Text);
                ddPresetSelect.Items.Remove(selectedItem);
                ddPresetSelect.SelectedIndex = 0;
            };
        }
    }
}
