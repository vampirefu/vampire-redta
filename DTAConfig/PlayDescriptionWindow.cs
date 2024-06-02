using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ClientGUI;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAConfig;
public class PlayDescriptionWindow : XNAWindow
{
    public XNAListBox lblPlayDesctiptionList;

    public PlayDescriptionWindow(WindowManager windowManager) : base(windowManager)
    {
    }

    public override void Initialize()
    {
        Name = "PlayDesctiptionWindow";
        ClientRectangle = new Rectangle(0, 0, 334, 453);

        var lblHeader = new XNALabel(WindowManager);
        lblHeader.Name = "lblCheater";
        lblHeader.ClientRectangle = new Rectangle(0, 0, 0, 0);
        lblHeader.FontIndex = 1;
        lblHeader.Text = "玩法介绍";

        lblPlayDesctiptionList = new XNAListBox(WindowManager);
        lblPlayDesctiptionList.Name = nameof(lblPlayDesctiptionList);
        lblPlayDesctiptionList.ClientRectangle = new Rectangle(30, lblHeader.Y + 60, 280, 350);
        lblPlayDesctiptionList.FontIndex = 1;
        lblPlayDesctiptionList.LineHeight = 30;

        var btnClose = new XNAClientButton(WindowManager);
        btnClose.Name = "btnClose";
        btnClose.ClientRectangle = new Rectangle((Width - UIDesignConstants.BUTTON_WIDTH_92) / 2,
            Height - 35, UIDesignConstants.BUTTON_WIDTH_92, UIDesignConstants.BUTTON_HEIGHT);
        btnClose.Text = "我知道了";
        btnClose.LeftClick += BtnYes_LeftClick;

        AddChild(lblPlayDesctiptionList);
        AddChild(lblHeader);
        AddChild(btnClose);

        lblHeader.CenterOnParent();
        lblHeader.ClientRectangle = new Rectangle(lblHeader.X, 12,
            lblHeader.Width, lblHeader.Height);

        base.Initialize();
    }

    public void AddDescription(string playDescription)
    {
        if (playDescription == null)
            return;

        lblPlayDesctiptionList.Items.Clear();

        var splitStrs = playDescription.Split('#');
        foreach (var item in splitStrs)
        {
            lblPlayDesctiptionList.AddItem(item);
        }
    }


    private void BtnYes_LeftClick(object sender, EventArgs e)
    {
        Disable();
    }
}
