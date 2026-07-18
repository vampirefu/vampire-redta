using System;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace ClientGUI
{
    /// <summary>
    /// 这是复选框和标准按钮的组合。必须指定Checked和Unchecked纹理
    /// 以渲染每种按钮状态。
    /// </summary>
    public class XNAClientToggleButton : XNAButton
    {
        public Texture2D CheckedTexture { get; set; }
        public Texture2D UncheckedTexture { get; set; }

        private string _toolTipText { get; set; }
        private ToolTip _toolTip { get; set; }

        private bool _checked { get; set; }

        public override void Initialize()
        {
            if (CheckedTexture == null)
                throw new ArgumentNullException(nameof(CheckedTexture));

            if (UncheckedTexture == null)
                throw new ArgumentNullException(nameof(UncheckedTexture));

            UpdateIdleTexture();

            if (HoverSoundEffect == null)
                HoverSoundEffect = new EnhancedSoundEffect("button.wav");

            base.Initialize();

            _toolTip = new ToolTip(WindowManager, this);
            SetToolTipText(_toolTipText);

            if (Width == 0)
                Width = IdleTexture.Width;
        }

        public bool Checked
        {
            get => _checked;
            set
            {
                _checked = value;
                UpdateIdleTexture();
            }
        }

        private void UpdateIdleTexture()
        {
            IdleTexture = _checked ? CheckedTexture : UncheckedTexture;
        }

        public void SetToolTipText(string text)
        {
            _toolTipText = text ?? string.Empty;
            if (_toolTip != null)
                _toolTip.Text = _toolTipText;
        }

        public XNAClientToggleButton(WindowManager windowManager) : base(windowManager)
        {
        }
    }
}
