using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using ClientCore;
using ClientGUI;
using DTAClient.DXGUI.Generic;
using Localization;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAConfig.OptionPanels;
internal class AboutOptionPanel : XNAOptionsPanel
{
    public AboutOptionPanel(WindowManager windowManager, UserINISettings iniSettings) : base(windowManager, iniSettings)
    {
    }

    private const string ModVersion = "1.1.0";
    /// <summary>
    /// 当前版本
    /// </summary>
    private XNALabel lblcurModVersion;
    /// <summary>
    /// Github源码地址
    /// </summary>
    private XNALinkLabel lblGithubUrl;
    /// <summary>
    /// 鸣谢列表标签
    /// </summary>
    private XNALinkLabel lblThankList;
    /// <summary>
    /// 鸣谢列表窗体
    /// </summary>
    private ThankWindow thankWindow;

    public override void Initialize()
    {
        base.Initialize();
        Name = nameof(AboutOptionPanel);

        //初始化鸣谢窗体
        thankWindow = new ThankWindow(WindowManager);
        AddAndInitializeWithControl(WindowManager, thankWindow);
        thankWindow.Disable();

        lblcurModVersion = new XNALabel(WindowManager);
        lblcurModVersion.Name = nameof(lblcurModVersion);
        lblcurModVersion.Text = $"当前Mod版本：{ModVersion}";
        lblcurModVersion.ClientRectangle = new Rectangle(20, 14, 0, 0);

        lblGithubUrl = new XNALinkLabel(WindowManager);
        lblGithubUrl.Name = nameof(lblGithubUrl);
        lblGithubUrl.Text = $"DTA源码地址";
        lblGithubUrl.ClientRectangle = new Rectangle(lblcurModVersion.X, lblcurModVersion.Y + 50, lblGithubUrl.Width, lblGithubUrl.Height);
        lblGithubUrl.LeftClick += (s, e) =>
        {
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "https://github.com/vampirefu/vampire-redta",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception e)
                {
                }
            });
        };

        lblThankList = new XNALinkLabel(WindowManager);
        lblThankList.Name = nameof(lblThankList);
        lblThankList.ClientRectangle = new Rectangle(lblGithubUrl.X, lblGithubUrl.Y + 52, 0, 0);
        lblThankList.Text = "Thanks".L10N("UI:DTAConfig:ButtonThanks");
        lblThankList.LeftClick += btnThank_LeftClick;

        AddChild(lblcurModVersion);
        AddChild(lblGithubUrl);
        AddChild(lblThankList);
    }

    public static void AddAndInitializeWithControl(WindowManager wm, XNAControl control)
    {
        var dp = new DarkeningPanel(wm);
        wm.AddAndInitializeControl(dp);
        dp.AddChild(control);
    }

    private void btnThank_LeftClick(object sender, EventArgs e)
    {
        thankWindow.CenterOnParent();
        thankWindow.Enable();
    }
}
