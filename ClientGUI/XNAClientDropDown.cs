using Rampastring.XNAUI.XNAControls;
using Rampastring.XNAUI;
using Rampastring.Tools;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClientGUI
{
    public class XNAClientDropDown : XNADropDown
    {
        public ToolTip ToolTip { get; set; }

        private int _scrollIndex = 0;
        private const int MAX_VISIBLE_ITEMS = 10;
        private int _clientHoveredIndex = -1;
        private Texture2D _scrollBarTexture;
        private bool _ignoreNextClick = false; // 新增标志：忽略下一次点击（用于防止刚打开时立即关闭）

        public XNAClientDropDown(WindowManager windowManager) : base(windowManager)
        {
        }

        private void CreateToolTip()
        {
            if (ToolTip == null)
                ToolTip = new ToolTip(WindowManager, this);
        }

        public override void Initialize()
        {
            ClickSoundEffect = new EnhancedSoundEffect("dropdown.wav");

            CreateToolTip();

            base.Initialize();

            // 创建一个1x1的白色纹理用于绘制滚动条
            _scrollBarTexture = AssetLoader.CreateTexture(Color.White, 1, 1);
        }

        public override void ParseAttributeFromINI(IniFile iniFile, string key, string value)
        {
            if (key == "ToolTip")
            {
                CreateToolTip();
                ToolTip.Text = value.Replace("@", Environment.NewLine);
                return;
            }
            if (key == "$ToolTip")
            {
                CreateToolTip();
                ToolTip.Text = string.Empty.Replace("@", Environment.NewLine);
                return;
            }
            base.ParseAttributeFromINI(iniFile, key, value);
        }

        public override void OnMouseLeftDown()
        {
            // 记录调用基类前的状态
            bool previouslyClosed = DropDownState == DropDownState.CLOSED;

            base.OnMouseLeftDown();
            UpdateToolTipBlock();

            // 如果展开且条目过多，限制高度
            if (DropDownState != DropDownState.CLOSED && Items.Count > MAX_VISIBLE_ITEMS)
            {
                // 如果是从关闭状态变为打开状态，标记忽略紧接着的松开事件
                if (previouslyClosed)
                {
                    _ignoreNextClick = true;
                }

                int newHeight = DropDownTexture.Height + 2 + (MAX_VISIBLE_ITEMS * ItemHeight);
                
                if (OpenUp)
                {
                    // 向上展开时，需要保持底部位置不变，重新计算Y
                    int bottom = Y + Height;
                    Height = newHeight;
                    Y = bottom - Height;
                }
                else
                {
                    Height = newHeight;
                }
            }
            else
            {
                _scrollIndex = 0;
            }
        }

        public override void OnMouseScrolled()
        {
            // 关闭状态下保持基类行为（切换选中项）
            if (DropDownState == DropDownState.CLOSED)
            {
                base.OnMouseScrolled();
                return;
            }

            // 展开状态下，如果条目不够多，不需要滚动
            if (Items.Count <= MAX_VISIBLE_ITEMS)
                return;

            // 展开状态下，滚轮用于滚动列表视图
            if (Cursor.ScrollWheelValue < 0) // 向下滚动
            {
                if (_scrollIndex < Items.Count - MAX_VISIBLE_ITEMS)
                    _scrollIndex++;
            }
            else if (Cursor.ScrollWheelValue > 0) // 向上滚动
            {
                if (_scrollIndex > 0)
                    _scrollIndex--;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // 自定义悬停逻辑（仅在有滚动条时启用）
            if (DropDownState != DropDownState.CLOSED && Items.Count > MAX_VISIBLE_ITEMS)
            {
                _clientHoveredIndex = -1;
                Point p = GetCursorPoint();

                if (p.X >= 0 && p.X <= Width && p.Y >= 0 && p.Y <= Height)
                {
                    int listStartY = (DropDownState == DropDownState.OPENED_DOWN) ? DropDownTexture.Height + 1 : 0;
                    int listHeight = MAX_VISIBLE_ITEMS * ItemHeight;

                    // 检查鼠标是否在列表区域内
                    if (p.Y >= listStartY && p.Y < listStartY + listHeight)
                    {
                        int relativeY = p.Y - listStartY;
                        int idx = relativeY / ItemHeight;
                        int actualIndex = _scrollIndex + idx;
                        
                        // 排除滚动条区域（假设滚动条宽度为10）
                        int scrollBarWidth = 10;
                        if (p.X < Width - scrollBarWidth)
                        {
                            if (actualIndex < Items.Count && Items[actualIndex].Selectable)
                            {
                                _clientHoveredIndex = actualIndex;
                            }
                        }
                    }
                }
            }
        }

        public override void OnLeftClick()
        {
            // 如果没有滚动条，使用基类点击逻辑
            if (DropDownState == DropDownState.CLOSED || Items.Count <= MAX_VISIBLE_ITEMS)
            {
                base.OnLeftClick();
                return;
            }

            // 有滚动条时的自定义点击逻辑
            if (_clientHoveredIndex != -1)
            {
                // 如果鼠标在条目上松开，则选中该条目（即使是刚打开）
                SelectedIndex = _clientHoveredIndex;
                ClickSoundEffect?.Play();
                CloseDropDown();
                _ignoreNextClick = false;
            }
            else
            {
                // 如果刚打开且没有选中条目，忽略这次点击（防止立即关闭）
                if (_ignoreNextClick)
                {
                    _ignoreNextClick = false;
                    return;
                }

                // 检查是否点击了头部（Header），如果是则关闭
                Point p = GetCursorPoint();
                bool inHeader = (DropDownState == DropDownState.OPENED_DOWN) ? 
                    p.Y < DropDownTexture.Height : 
                    p.Y > Height - DropDownTexture.Height;

                if (inHeader)
                {
                    CloseDropDown();
                }
            }
        }

        protected override void CloseDropDown()
        {
            base.CloseDropDown();
            UpdateToolTipBlock();
            _scrollIndex = 0;
            _ignoreNextClick = false;
        }

        protected void UpdateToolTipBlock()
        {
            if (DropDownState == DropDownState.CLOSED)
                ToolTip.Blocked = false;
            else
                ToolTip.Blocked = true;
        }

        public override void Draw(GameTime gameTime)
        {
            // 如果没有滚动条，直接使用基类绘制
            if (DropDownState == DropDownState.CLOSED || Items.Count <= MAX_VISIBLE_ITEMS)
            {
                base.Draw(gameTime);
                return;
            }

            // === 自定义绘制逻辑（带滚动条） ===

            // 1. 绘制头部（Header）
            Rectangle dropDownRect;
            if (DropDownState == DropDownState.OPENED_DOWN)
                dropDownRect = new Rectangle(0, 0, Width, DropDownTexture.Height);
            else
                dropDownRect = new Rectangle(0, Height - DropDownTexture.Height, Width, DropDownTexture.Height);

            FillRectangle(new Rectangle(dropDownRect.X + 1, dropDownRect.Y + 1,
                dropDownRect.Width - 2, dropDownRect.Height - 2), BackColor);
            DrawRectangle(dropDownRect, BorderColor);

            // 绘制头部选中的文本
            if (SelectedIndex > -1 && SelectedIndex < Items.Count)
            {
                var item = Items[SelectedIndex];
                int textX = 3;
                if (item.Texture != null)
                {
                    DrawTexture(item.Texture,
                        new Rectangle(1, dropDownRect.Y + 2,
                        item.Texture.Width, item.Texture.Height), Color.White);
                    textX += item.Texture.Width + 1;
                }

                if (item.Text != null)
                {
                    DrawStringWithShadow(item.Text, FontIndex,
                        new Vector2(textX, dropDownRect.Y + 2), GetItemTextColor(item));
                }
            }

            if (AllowDropDown)
            {
                // 绘制箭头
                var ddRectangle = new Rectangle(Width - DropDownTexture.Width,
                    dropDownRect.Y, DropDownTexture.Width, DropDownTexture.Height);

                DrawTexture(DropDownOpenTexture, ddRectangle, RemapColor);

                // 2. 绘制列表背景和边框
                Rectangle listRectangle;
                if (DropDownState == DropDownState.OPENED_DOWN)
                    listRectangle = new Rectangle(0, DropDownTexture.Height, Width, Height - DropDownTexture.Height);
                else
                    listRectangle = new Rectangle(0, 0, Width, Height - DropDownTexture.Height);

                DrawRectangle(listRectangle, BorderColor);

                int scrollBarWidth = 10;
                int itemWidth = Width - 2 - scrollBarWidth;

                // 3. 绘制可见条目
                for (int i = 0; i < MAX_VISIBLE_ITEMS; i++)
                {
                    int itemIndex = _scrollIndex + i;
                    if (itemIndex >= Items.Count) break;

                    int y = listRectangle.Y + 1 + i * ItemHeight;
                    var item = Items[itemIndex];

                    // 绘制条目背景（高亮或普通）
                    if (_clientHoveredIndex == itemIndex)
                        FillRectangle(new Rectangle(1, y, itemWidth, ItemHeight), FocusColor);
                    else
                        FillRectangle(new Rectangle(1, y, itemWidth, ItemHeight), BackColor);

                    // 绘制条目内容
                    int textX = 2;
                    if (item.Texture != null)
                    {
                        DrawTexture(item.Texture, new Rectangle(1, y + 1, item.Texture.Width, item.Texture.Height), Color.White);
                        textX += item.Texture.Width + 1;
                    }

                    Color textColor = item.Selectable ? GetItemTextColor(item) : DisabledItemColor;
                    if (item.Text != null)
                        DrawStringWithShadow(item.Text, FontIndex, new Vector2(textX, y + 1), textColor);
                }

                // 4. 绘制滚动条
                int scrollBarX = Width - scrollBarWidth - 1;
                int scrollBarY = listRectangle.Y + 1;
                int scrollBarHeight = listRectangle.Height - 2;

                // 滚动条背景（轨道）
                DrawTexture(_scrollBarTexture, new Rectangle(scrollBarX, scrollBarY, scrollBarWidth, scrollBarHeight), Color.Black * 0.5f);

                // 计算滑块位置和大小
                int totalItems = Items.Count;
                float viewRatio = (float)MAX_VISIBLE_ITEMS / totalItems;
                int thumbHeight = (int)(scrollBarHeight * viewRatio);
                if (thumbHeight < 10) thumbHeight = 10;

                float scrollRatio = (float)_scrollIndex / (totalItems - MAX_VISIBLE_ITEMS);
                int maxThumbY = scrollBarHeight - thumbHeight;
                int thumbY = scrollBarY + (int)(maxThumbY * scrollRatio);

                // 绘制滑块
                DrawTexture(_scrollBarTexture, new Rectangle(scrollBarX, thumbY, scrollBarWidth, thumbHeight), Color.Gray);
            }
        }
    }
}
