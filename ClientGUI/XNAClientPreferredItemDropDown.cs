using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System.Collections.Generic;

namespace ClientGUI
{
    /// <summary>
    /// 具有首选下拉项的下拉控件，带有可选的字符串标签显示在其文本旁边。
    /// </summary>
    public class XNAClientPreferredItemDropDown : XNAClientDropDown
    {
        /// <summary>
        /// 显示在首选下拉项文本旁边的字符串标签。
        /// </summary>
        public string PreferredItemLabel { get; set; }

        /// <summary>
        /// 首选下拉项的索引。
        /// </summary>
        public List<int> PreferredItemIndexes { get; set; } = new List<int>();

        /// <summary>
        /// 创建新的首选项下拉控件。
        /// </summary>
        /// <param name="windowManager">与此控件关联的WindowManager。</param>
        public XNAClientPreferredItemDropDown(WindowManager windowManager) : base(windowManager)
        {
        }

        public override void ParseAttributeFromINI(IniFile iniFile, string key, string value)
        {
            switch (key)
            {
                case "PreferredItemLabel":
                    PreferredItemLabel = value;
                    return;
            }

            base.ParseAttributeFromINI(iniFile, key, value);
        }

        /// <summary>
        /// 绘制下拉框。
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            if (PreferredItemIndexes.Count > 0)
            {
                PreferredItemIndexes.ForEach(i =>
                {
                    XNADropDownItem preferredItem = Items[i];
                    string preferredItemOriginalText = preferredItem.Text;
                    preferredItem.Text += " " + PreferredItemLabel;
                });

                base.Draw(gameTime);

                PreferredItemIndexes.ForEach(i =>
                {
                    XNADropDownItem preferredItem = Items[i];
                    preferredItem.Text = preferredItem.Text.Substring(0, preferredItem.Text.Length - PreferredItemLabel.Length - 1);
                });
            }
            else
            {
                base.Draw(gameTime);
            }

        }
    }
}
