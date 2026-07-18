using System;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI.XNAControls;
using Rampastring.XNAUI;
using Microsoft.Xna.Framework.Input;

namespace ClientGUI
{
    /// <summary>
    /// 带有"确定"或"是/否"或"确定/取消"按钮的通用消息框。
    /// </summary>
    public class XNAMessageBox : XNAWindow
    {
        /// <summary>
        /// 创建新的消息框。
        /// </summary>
        /// <param name="windowManager">窗口管理器。</param>
        /// <param name="caption">消息框的标题。</param>
        /// <param name="description">消息框的实际消息。</param>
        /// <param name="messageBoxButtons">定义对话框中可用的按钮。</param>
        public XNAMessageBox(WindowManager windowManager,
            string caption, string description, XNAMessageBoxButtons messageBoxButtons)
            : base(windowManager)
        {
            this.caption = caption;
            this.description = description;
            this.messageBoxButtons = messageBoxButtons;
        }

        /// <summary>
        /// 当用户点击消息框上的"确定"按钮时调用的方法。
        /// </summary>
        public Action<XNAMessageBox> OKClickedAction { get; set; }

        /// <summary>
        /// 当用户点击消息框上的"是"按钮时调用的方法。
        /// </summary>
        public Action<XNAMessageBox> YesClickedAction { get; set; }

        /// <summary>
        /// 当用户点击消息框上的"否"按钮时调用的方法。
        /// </summary>
        public Action<XNAMessageBox> NoClickedAction { get; set; }

        /// <summary>
        /// 当用户点击消息框上的"取消"按钮时调用的方法。
        /// </summary>
        public Action<XNAMessageBox> CancelClickedAction { get; set; }


        private string caption;
        private string description;
        private XNAMessageBoxButtons messageBoxButtons;

        public override void Initialize()
        {
            Name = "MessageBox";
            BackgroundTexture = AssetLoader.LoadTexture("msgboxform.png");

            XNALabel lblCaption = new XNALabel(WindowManager);
            lblCaption.Text = caption;
            lblCaption.ClientRectangle = new Rectangle(12, 9, 0, 0);
            lblCaption.FontIndex = 1;

            XNAPanel line = new XNAPanel(WindowManager);
            line.ClientRectangle = new Rectangle(6, 29, 0, 1);

            XNALabel lblDescription = new XNALabel(WindowManager);
            lblDescription.Text = description;
            lblDescription.ClientRectangle = new Rectangle(12, 39, 0, 0);

            AddChild(lblCaption);
            AddChild(line);
            AddChild(lblDescription);

            Vector2 textDimensions = Renderer.GetTextDimensions(lblDescription.Text, lblDescription.FontIndex);
            ClientRectangle = new Rectangle(0, 0, (int)textDimensions.X + 24, (int)textDimensions.Y + 81);
            line.ClientRectangle = new Rectangle(6, 29, Width - 12, 1);

            if (messageBoxButtons == XNAMessageBoxButtons.OK)
            {
                AddOKButton();
            }
            else if (messageBoxButtons == XNAMessageBoxButtons.YesNo)
            {
                AddYesNoButtons();
            }
            else // messageBoxButtons == DXMessageBoxButtons.OKCancel
            {
                AddOKCancelButtons();
            }

            base.Initialize();

            WindowManager.CenterControlOnScreen(this);
        }

        private void AddOKButton()
        {
            XNAButton btnOK = new XNAButton(WindowManager);
            btnOK.FontIndex = 1;
            btnOK.ClientRectangle = new Rectangle(0, 0, 75, 23);
            btnOK.IdleTexture = AssetLoader.LoadTexture("75pxbtn.png");
            btnOK.HoverTexture = AssetLoader.LoadTexture("75pxbtn_c.png");
            btnOK.HoverSoundEffect = new EnhancedSoundEffect("button.wav");
            btnOK.Name = "btnOK";
            btnOK.Text = "确定";
            btnOK.LeftClick += BtnOK_LeftClick;
            btnOK.HotKey = Keys.Enter;

            AddChild(btnOK);

            btnOK.CenterOnParent();
            btnOK.ClientRectangle = new Rectangle(btnOK.X,
                Height - 28, btnOK.Width, btnOK.Height);
        }

        private void AddYesNoButtons()
        {
            XNAButton btnYes = new XNAButton(WindowManager);
            btnYes.FontIndex = 1;
            btnYes.ClientRectangle = new Rectangle(0, 0, 75, 23);
            btnYes.IdleTexture = AssetLoader.LoadTexture("75pxbtn.png");
            btnYes.HoverTexture = AssetLoader.LoadTexture("75pxbtn_c.png");
            btnYes.HoverSoundEffect = new EnhancedSoundEffect("button.wav");
            btnYes.Name = "btnYes";
            btnYes.Text = "是";
            btnYes.LeftClick += BtnYes_LeftClick;
            btnYes.HotKey = Keys.Y;

            AddChild(btnYes);

            btnYes.ClientRectangle = new Rectangle((Width - ((btnYes.Width + 5) * 2)) / 2,
                Height - 28, btnYes.Width, btnYes.Height);

            XNAButton btnNo = new XNAButton(WindowManager);
            btnNo.FontIndex = 1;
            btnNo.ClientRectangle = new Rectangle(0, 0, 75, 23);
            btnNo.IdleTexture = AssetLoader.LoadTexture("75pxbtn.png");
            btnNo.HoverTexture = AssetLoader.LoadTexture("75pxbtn_c.png");
            btnNo.HoverSoundEffect = new EnhancedSoundEffect("button.wav");
            btnNo.Name = "btnNo";
            btnNo.Text = "否";
            btnNo.LeftClick += BtnNo_LeftClick;
            btnNo.HotKey = Keys.N;

            AddChild(btnNo);

            btnNo.ClientRectangle = new Rectangle(btnYes.X + btnYes.Width + 10,
                Height - 28, btnNo.Width, btnNo.Height);
        }

        private void AddOKCancelButtons()
        {
            XNAButton btnOK = new XNAButton(WindowManager);
            btnOK.FontIndex = 1;
            btnOK.ClientRectangle = new Rectangle(0, 0, 75, 23);
            btnOK.IdleTexture = AssetLoader.LoadTexture("75pxbtn.png");
            btnOK.HoverTexture = AssetLoader.LoadTexture("75pxbtn_c.png");
            btnOK.HoverSoundEffect = new EnhancedSoundEffect("button.wav");
            btnOK.Name = "btnOK";
            btnOK.Text = "确定";
            btnOK.LeftClick += BtnYes_LeftClick;
            btnOK.HotKey = Keys.Enter;

            AddChild(btnOK);

            btnOK.ClientRectangle = new Rectangle((Width - ((btnOK.Width + 5) * 2)) / 2,
                Height - 28, btnOK.Width, btnOK.Height);

            XNAButton btnCancel = new XNAButton(WindowManager);
            btnCancel.FontIndex = 1;
            btnCancel.ClientRectangle = new Rectangle(0, 0, 75, 23);
            btnCancel.IdleTexture = AssetLoader.LoadTexture("75pxbtn.png");
            btnCancel.HoverTexture = AssetLoader.LoadTexture("75pxbtn_c.png");
            btnCancel.HoverSoundEffect = new EnhancedSoundEffect("button.wav");
            btnCancel.Name = "btnCancel";
            btnCancel.Text = "取消";
            btnCancel.LeftClick += BtnCancel_LeftClick;
            btnCancel.HotKey = Keys.C;

            AddChild(btnCancel);

            btnCancel.ClientRectangle = new Rectangle(btnOK.X + btnOK.Width + 10,
                Height - 28, btnCancel.Width, btnCancel.Height);
        }

        private void BtnOK_LeftClick(object sender, EventArgs e)
        {
            Hide();
            OKClickedAction?.Invoke(this);
        }

        private void BtnYes_LeftClick(object sender, EventArgs e)
        {
            Hide();
            YesClickedAction?.Invoke(this);
        }

        private void BtnNo_LeftClick(object sender, EventArgs e)
        {
            Hide();
            NoClickedAction?.Invoke(this);
        }

        private void BtnCancel_LeftClick(object sender, EventArgs e)
        {
            Hide();
            CancelClickedAction?.Invoke(this);
        }

        private void Hide()
        {
            if (this.Parent != null)
                WindowManager.RemoveControl(this.Parent);
            else
                WindowManager.RemoveControl(this);
        }

        public void Show()
        {
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, this);
        }

        #region 静态Show方法

        /// <summary>
        /// 创建并显示具有指定标题和描述的新消息框。
        /// </summary>
        /// <param name="game">游戏。</param>
        /// <param name="caption">消息框的标题/头部。</param>
        /// <param name="description">消息框的描述。</param>
        public static void Show(WindowManager windowManager, string caption, string description)
        {
            var panel = new DarkeningPanel(windowManager);
            panel.Focused = true;
            windowManager.AddAndInitializeControl(panel);

            var msgBox = new XNAMessageBox(windowManager,
                Renderer.GetSafeString(caption, 1), 
                Renderer.GetSafeString(description, 0), 
                XNAMessageBoxButtons.OK);

            panel.AddChild(msgBox);
            msgBox.OKClickedAction = MsgBox_OKClicked;
            windowManager.AddAndInitializeControl(msgBox);
            windowManager.SelectedControl = null;
        }

        private static void MsgBox_OKClicked(XNAMessageBox messageBox)
        {
            var parent = (DarkeningPanel)messageBox.Parent;
            parent.Hide();
            parent.Hidden += Parent_Hidden;
        }

        /// <summary>
        /// 显示消息框，用户输入选项为"是"和"否"。
        /// </summary>
        /// <param name="windowManager">WindowManager。</param>
        /// <param name="caption">消息框的标题。</param>
        /// <param name="description">消息框中的描述。</param>
        /// <returns>创建的XNAMessageBox实例。</returns>
        public static XNAMessageBox ShowYesNoDialog(WindowManager windowManager, string caption, string description)
        {
            var panel = new DarkeningPanel(windowManager);
            windowManager.AddAndInitializeControl(panel);

            var msgBox = new XNAMessageBox(windowManager,
                Renderer.GetSafeString(caption, 1),
                Renderer.GetSafeString(description, 0),
                XNAMessageBoxButtons.YesNo);

            panel.AddChild(msgBox);
            msgBox.YesClickedAction = MsgBox_YesClicked;
            msgBox.NoClickedAction = MsgBox_NoClicked;

            return msgBox;
        }

        private static void MsgBox_NoClicked(XNAMessageBox messageBox)
        {
            var parent = (DarkeningPanel)messageBox.Parent;
            parent.Hide();
            parent.Hidden += Parent_Hidden;
        }

        private static void MsgBox_YesClicked(XNAMessageBox messageBox)
        {
            var parent = (DarkeningPanel)messageBox.Parent;
            parent.Hide();
            parent.Hidden += Parent_Hidden;
        }

        private static void Parent_Hidden(object sender, EventArgs e)
        {
            var darkeningPanel = (DarkeningPanel)sender;

            darkeningPanel.WindowManager.RemoveControl(darkeningPanel);
            darkeningPanel.Hidden -= Parent_Hidden;
        }

        #endregion
    }

    public enum XNAMessageBoxButtons
    {
        OK,
        YesNo,
        OKCancel
    }
}
