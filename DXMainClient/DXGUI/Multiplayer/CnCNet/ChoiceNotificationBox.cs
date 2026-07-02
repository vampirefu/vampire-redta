using ClientGUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.IO;
using System.Reflection;
using ClientCore.CnCNet5;
using SixLabors.ImageSharp;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    /// <summary>
    /// 允许用户做出选择的通知框，
    /// 位于游戏窗口左上角。
    /// </summary>
    public class ChoiceNotificationBox : XNAPanel
    {
        private const double DOWN_TIME_WAIT_SECONDS = 4.0;
        private const double DOWN_MOVEMENT_RATE = 2.0;
        private const double UP_MOVEMENT_RATE = 2.0;

        public ChoiceNotificationBox(WindowManager windowManager) : base(windowManager)
        {
            downTimeWaitTime = TimeSpan.FromSeconds(DOWN_TIME_WAIT_SECONDS);
        }

        public Action<ChoiceNotificationBox> AffirmativeClickedAction { get; set; }
        public Action<ChoiceNotificationBox> NegativeClickedAction { get; set; }

        private XNALabel lblHeader;
        private XNAPanel gameIconPanel;
        private XNALabel lblSender;
        private XNALabel lblChoiceText;
        private XNAClientButton affirmativeButton;
        private XNAClientButton negativeButton;

        private TimeSpan downTime = TimeSpan.Zero;

        private TimeSpan downTimeWaitTime;

        private bool isDown = false;

        private const int boxHeight = 101;

        private double locationY = -boxHeight;

        public override void Initialize()
        {
            Name = nameof(ChoiceNotificationBox);
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 196), 1, 1);
            ClientRectangle = new Rectangle(0, -boxHeight, 300, boxHeight);
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            lblHeader = new XNALabel(WindowManager);
            lblHeader.Name = nameof(lblHeader);
            lblHeader.FontIndex = 1;
            lblHeader.AnchorPoint = new Vector2(ClientRectangle.Width / 2, 12);
            lblHeader.TextAnchor = LabelTextAnchorInfo.CENTER;
            lblHeader.Text = "作出选择";
            AddChild(lblHeader);

            using Stream dtaIconStream = Assembly.GetAssembly(typeof(GameCollection)).GetManifestResourceStream("ClientCore.Resources.dtaicon.png");
            using var dtaIcon = Image.Load(dtaIconStream);

            gameIconPanel = new XNAPanel(WindowManager);
            gameIconPanel.Name = nameof(gameIconPanel);
            gameIconPanel.ClientRectangle = new Rectangle(12, lblHeader.Bottom + 6, 16, 16);
            gameIconPanel.DrawBorders = false;
            gameIconPanel.BackgroundTexture = AssetLoader.TextureFromImage(dtaIcon);
            AddChild(gameIconPanel);

            lblSender = new XNALabel(WindowManager);
            lblSender.Name = nameof(lblSender);
            lblSender.FontIndex = 1;
            lblSender.ClientRectangle = new Rectangle(gameIconPanel.Right + 3, lblHeader.Bottom + 6, 0, 0);
            lblSender.Text = "发送者";
            AddChild(lblSender);

            lblChoiceText = new XNALabel(WindowManager);
            lblChoiceText.Name = nameof(lblChoiceText);
            lblChoiceText.FontIndex = 1;
            lblChoiceText.ClientRectangle = new Rectangle(12, lblSender.Bottom + 6, 0, 0);
            lblChoiceText.Text = "您想要做什么?";
            AddChild(lblChoiceText);

            affirmativeButton = new XNAClientButton(WindowManager);
            affirmativeButton.ClientRectangle = new Rectangle(ClientRectangle.Left + 8, lblChoiceText.Bottom + 6, 75, 23);
            affirmativeButton.Name = nameof(affirmativeButton);
            affirmativeButton.Text = "确定";
            affirmativeButton.LeftClick += AffirmativeButton_LeftClick;
            AddChild(affirmativeButton);

            negativeButton = new XNAClientButton(WindowManager);
            negativeButton.ClientRectangle = new Rectangle(ClientRectangle.Width - (75 + 8), lblChoiceText.Bottom + 6, 75, 23);
            negativeButton.Name = nameof(negativeButton);
            negativeButton.Text = "取消";
            negativeButton.LeftClick += NegativeButton_LeftClick;
            AddChild(negativeButton);

            base.Initialize();
        }

        // 超时时间为零表示通知永远不会被自动关闭
        public void Show(
            string headerText,
            Texture2D gameIcon,
            string sender,
            string choiceText,
            string affirmativeText,
            string negativeText,
            int timeout = 0)
        {
            Enable();

            lblHeader.Text = headerText;
            gameIconPanel.BackgroundTexture = gameIcon;
            lblSender.Text = sender;
            lblChoiceText.Text = choiceText;
            affirmativeButton.Text = affirmativeText;
            negativeButton.Text = negativeText;

            // 使用与私聊通知相同的裁剪逻辑
            if (lblChoiceText.Width > Width)
            {
                while (lblChoiceText.Width > Width)
                {
                    lblChoiceText.Text = lblChoiceText.Text.Remove(lblChoiceText.Text.Length - 1);
                }
            }

            downTime = TimeSpan.Zero;
            isDown = true;

            downTimeWaitTime = TimeSpan.FromSeconds(timeout);
        }

        public void Hide()
        {
            isDown = false;
            locationY = -Height;
            ClientRectangle = new Rectangle(X, (int)locationY,
                Width, Height);
            Disable();
        }

        public override void Update(GameTime gameTime)
        {
            if (isDown)
            {
                if (locationY < 0)
                {
                    locationY += DOWN_MOVEMENT_RATE;
                    ClientRectangle = new Rectangle(X, (int)locationY,
                        Width, Height);
                }

                if (WindowManager.HasFocus)
                {
                    downTime += gameTime.ElapsedGameTime;

                    // 仅在我们有有效超时时间时更改"按下"状态
                    if (downTimeWaitTime != TimeSpan.Zero)
                    {
                        isDown = downTime < downTimeWaitTime;
                    }
                }
            }
            else
            {
                if (locationY > -Height)
                {
                    locationY -= UP_MOVEMENT_RATE;
                    ClientRectangle = new Rectangle(X, (int)locationY, Width, Height);
                }
                else
                {
                    // 当超时后实际上删除自身
                    WindowManager.RemoveControl(this);
                }
            }

            base.Update(gameTime);
        }

        private void AffirmativeButton_LeftClick(object sender, EventArgs e)
        {
            AffirmativeClickedAction?.Invoke(this);
            WindowManager.RemoveControl(this);
        }

        private void NegativeButton_LeftClick(object sender, EventArgs e)
        {
            NegativeClickedAction?.Invoke(this);
            WindowManager.RemoveControl(this);
        }
    }
}
