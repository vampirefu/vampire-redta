using ClientCore;
using ClientGUI;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Generic
{
    /// <summary>
    /// A notification that asks the user to accept the CnCNet privacy policy.
    /// </summary>
    class PrivacyNotification : XNAWindow
    {
        public PrivacyNotification(WindowManager windowManager) : base(windowManager)
        {
            // DrawMode = ControlDrawMode.UNIQUE_RENDER_TARGET;
        }

        public override void Initialize()
        {
            Name = nameof(PrivacyNotification);
            Width = WindowManager.RenderResolutionX;

            var lblDescription = new XNALabel(WindowManager);
            lblDescription.Name = nameof(lblDescription);
            lblDescription.X = UIDesignConstants.EMPTY_SPACE_SIDES;
            lblDescription.Y = UIDesignConstants.EMPTY_SPACE_TOP;
            lblDescription.Text = Renderer.FixText(
                "使用此客户端即代表您同意CnCNet条款和条件以及CnCNet隐私政策.隐私相关设置可在客户端中设置.",
                lblDescription.FontIndex, WindowManager.RenderResolutionX - (UIDesignConstants.EMPTY_SPACE_SIDES * 2)).Text;
            AddChild(lblDescription);

            var lblMoreInformation = new XNALabel(WindowManager);
            lblMoreInformation.Name = nameof(lblMoreInformation);
            lblMoreInformation.X = lblDescription.X;
            lblMoreInformation.Y = lblDescription.Bottom + UIDesignConstants.CONTROL_VERTICAL_MARGIN;
            lblMoreInformation.Text = "更多信息:"+ " ";
            AddChild(lblMoreInformation);

            var lblTermsAndConditions = new XNALinkLabel(WindowManager);
            lblTermsAndConditions.Name = nameof(lblTermsAndConditions);
            lblTermsAndConditions.X = lblMoreInformation.Right + UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            lblTermsAndConditions.Y = lblMoreInformation.Y;
            lblTermsAndConditions.Text = "https://cncnet.org/terms-and-conditions";
            lblTermsAndConditions.LeftClick += (s, e) => ProcessLauncher.StartShellProcess(lblTermsAndConditions.Text);
            AddChild(lblTermsAndConditions);

            var lblPrivacyPolicy = new XNALinkLabel(WindowManager);
            lblPrivacyPolicy.Name = nameof(lblPrivacyPolicy);
            lblPrivacyPolicy.X = lblTermsAndConditions.Right + UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            lblPrivacyPolicy.Y = lblMoreInformation.Y;
            lblPrivacyPolicy.Text = "https://cncnet.org/privacy-policy";
            lblPrivacyPolicy.LeftClick += (s, e) => ProcessLauncher.StartShellProcess(lblPrivacyPolicy.Text);
            AddChild(lblPrivacyPolicy);

            var lblExplanation = new XNALabel(WindowManager);
            lblExplanation.Name = nameof(lblExplanation);
            lblExplanation.X = UIDesignConstants.EMPTY_SPACE_SIDES;
            lblExplanation.Y = lblMoreInformation.Bottom + UIDesignConstants.CONTROL_VERTICAL_MARGIN * 2;
            lblExplanation.Text = "不用担心,我们不会将您的数据用作不良用途,但根据相关法律我们必须显示此条信息.游戏是完全免费的,如果你是从付费渠道获得，那说明你被骗了！";
            lblExplanation.TextColor = UISettings.ActiveSettings.SubtleTextColor;
            AddChild(lblExplanation);

            var btnOK = new XNAClientButton(WindowManager);
            btnOK.Name = nameof(btnOK);
            btnOK.Width = 75;
            btnOK.Y = lblExplanation.Y;
            btnOK.X = WindowManager.RenderResolutionX - btnOK.Width - UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            btnOK.Text = "了解";
            AddChild(btnOK);
            btnOK.LeftClick += (s, e) => 
            {
                UserINISettings.Instance.PrivacyPolicyAccepted.Value = true;
                UserINISettings.Instance.SaveSettings();
                // AlphaRate = -0.2f;
                Disable(); 
            };

            Height = btnOK.Bottom + UIDesignConstants.EMPTY_SPACE_BOTTOM;
            Y = WindowManager.RenderResolutionY - Height;

            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Alpha <= 0.0)
                Disable();
        }
    }
}
