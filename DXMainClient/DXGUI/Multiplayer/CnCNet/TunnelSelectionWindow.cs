using ClientGUI;
using DTAClient.Domain.Multiplayer.CnCNet;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    /// <summary>
    /// 用于选择CnCNet隧道服务器的窗口。
    /// </summary>
    class TunnelSelectionWindow : XNAWindow
    {
        public TunnelSelectionWindow(WindowManager windowManager, TunnelHandler tunnelHandler) : base(windowManager)
        {
            this.tunnelHandler = tunnelHandler;
        }

        public event EventHandler<TunnelEventArgs> TunnelSelected;

        private readonly TunnelHandler tunnelHandler;
        private TunnelListBox lbTunnelList;
        private XNALabel lblDescription;
        private XNAClientButton btnApply;

        private string originalTunnelAddress;

        public override void Initialize()
        {
            if (Initialized)
                return;

            Name = "TunnelSelectionWindow";

            BackgroundTexture = AssetLoader.LoadTexture("gamecreationoptionsbg.png");
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            lblDescription = new XNALabel(WindowManager);
            lblDescription.Name = nameof(lblDescription);
            lblDescription.Text = "第1行" + Environment.NewLine + "第2行";
            lblDescription.X = UIDesignConstants.EMPTY_SPACE_SIDES + UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            lblDescription.Y = UIDesignConstants.EMPTY_SPACE_TOP + UIDesignConstants.CONTROL_VERTICAL_MARGIN;
            AddChild(lblDescription);

            lbTunnelList = new TunnelListBox(WindowManager, tunnelHandler);
            lbTunnelList.Name = nameof(lbTunnelList);
            lbTunnelList.Y = lblDescription.Bottom + UIDesignConstants.CONTROL_VERTICAL_MARGIN;
            lbTunnelList.X = UIDesignConstants.EMPTY_SPACE_SIDES + UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            AddChild(lbTunnelList);
            lbTunnelList.SelectedIndexChanged += LbTunnelList_SelectedIndexChanged;

            btnApply = new XNAClientButton(WindowManager);
            btnApply.Name = nameof(btnApply);
            btnApply.Width = UIDesignConstants.BUTTON_WIDTH_92;
            btnApply.Height = UIDesignConstants.BUTTON_HEIGHT;
            btnApply.Text = "应用";
            btnApply.X = UIDesignConstants.EMPTY_SPACE_SIDES + UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            btnApply.Y = lbTunnelList.Bottom + UIDesignConstants.CONTROL_VERTICAL_MARGIN * 3;
            AddChild(btnApply);
            btnApply.LeftClick += BtnApply_LeftClick;

            var btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Name = nameof(btnCancel);
            btnCancel.Width = UIDesignConstants.BUTTON_WIDTH_92;
            btnCancel.Height = UIDesignConstants.BUTTON_HEIGHT;
            btnCancel.Text = "取消";
            btnCancel.Y = btnApply.Y;
            AddChild(btnCancel);
            btnCancel.LeftClick += BtnCancel_LeftClick;

            Width = lbTunnelList.Right + UIDesignConstants.CONTROL_HORIZONTAL_MARGIN + UIDesignConstants.EMPTY_SPACE_SIDES;
            Height = btnApply.Bottom + UIDesignConstants.CONTROL_VERTICAL_MARGIN + UIDesignConstants.EMPTY_SPACE_BOTTOM;
            btnCancel.X = Width - btnCancel.Width - UIDesignConstants.EMPTY_SPACE_SIDES - UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;

            base.Initialize();
        }

        private void BtnApply_LeftClick(object sender, EventArgs e)
        {
            Disable();

            if (!lbTunnelList.IsValidIndexSelected())
                return;

            CnCNetTunnel tunnel = tunnelHandler.Tunnels[lbTunnelList.SelectedIndex];
            TunnelSelected?.Invoke(this, new TunnelEventArgs(tunnel));
        }

        private void BtnCancel_LeftClick(object sender, EventArgs e) => Disable();

        private void LbTunnelList_SelectedIndexChanged(object sender, EventArgs e) =>
            btnApply.AllowClick = !lbTunnelList.IsTunnelSelected(originalTunnelAddress) && lbTunnelList.IsValidIndexSelected();

        /// <summary>
        /// 设置窗口描述并选择具有给定地址的隧道服务器。
        /// </summary>
        /// <param name="description">窗口描述。</param>
        /// <param name="tunnelAddress">要选择的隧道服务器地址。</param>
        public void Open(string description, string tunnelAddress = null)
        {
            lblDescription.Text = description;
            originalTunnelAddress = tunnelAddress;

            if (!string.IsNullOrWhiteSpace(tunnelAddress))
                lbTunnelList.SelectTunnel(tunnelAddress);
            else
                lbTunnelList.SelectedIndex = -1;

            if (lbTunnelList.SelectedIndex > -1)
            {
                lbTunnelList.SetTopIndex(0);

                while (lbTunnelList.SelectedIndex > lbTunnelList.LastIndex)
                    lbTunnelList.TopIndex++;
            }

            btnApply.AllowClick = false;
            Enable();
        }
    }

    class TunnelEventArgs : EventArgs
    {
        public TunnelEventArgs(CnCNetTunnel tunnel)
        {
            Tunnel = tunnel;
        }

        public CnCNetTunnel Tunnel { get; }
    }
}
