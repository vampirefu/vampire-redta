using Rampastring.XNAUI.XNAControls;
using Rampastring.XNAUI;
using Rampastring.Tools;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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
        private bool _isDraggingThumb = false; // 是否正在拖拽滚动条滑块
        private int _dragThumbOffsetY = 0; // 拖拽时鼠标相对于滑块顶部的偏移量
        private bool _suppressNextClick = false; // 拖拽结束后抑制本次点击选择
        private const int SCROLL_BAR_WIDTH = 10;

        public XNAClientDropDown(WindowManager windowManager) : base(windowManager)
        {
        }

        private void CreateToolTip()
        {
            if (ToolTip == null)
                ToolTip = new ToolTip(WindowManager, this);
        }

        /// <summary>
        /// 计算滚动条的几何信息
        /// </summary>
        private void GetScrollBarGeometry(out int scrollBarX, out int scrollBarY, out int scrollBarHeight, out int thumbHeight, out int thumbY)
        {
            scrollBarX = Width - SCROLL_BAR_WIDTH - 1;

            Rectangle listRectangle;
            if (DropDownState == DropDownState.OPENED_DOWN)
                listRectangle = new Rectangle(0, DropDownTexture.Height, Width, Height - DropDownTexture.Height);
            else
                listRectangle = new Rectangle(0, 0, Width, Height - DropDownTexture.Height);

            scrollBarY = listRectangle.Y + 1;
            scrollBarHeight = listRectangle.Height - 2;

            int totalItems = Items.Count;
            float viewRatio = (float)MAX_VISIBLE_ITEMS / totalItems;
            thumbHeight = (int)(scrollBarHeight * viewRatio);
            if (thumbHeight < 10) thumbHeight = 10;

            float scrollRatio = (totalItems > MAX_VISIBLE_ITEMS) ? (float)_scrollIndex / (totalItems - MAX_VISIBLE_ITEMS) : 0f;
            int maxThumbY = scrollBarHeight - thumbHeight;
            thumbY = scrollBarY + (int)(maxThumbY * scrollRatio);
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
            // 检查是否点击了滚动条区域（展开状态且有滚动条时）
            if (DropDownState != DropDownState.CLOSED && Items.Count > MAX_VISIBLE_ITEMS)
            {
                Point p = GetCursorPoint();
                GetScrollBarGeometry(out int scrollBarX, out int scrollBarY, out int scrollBarHeight, out int thumbHeight, out int thumbY);

                if (p.X >= scrollBarX && p.X <= scrollBarX + SCROLL_BAR_WIDTH &&
                    p.Y >= scrollBarY && p.Y <= scrollBarY + scrollBarHeight)
                {
                    if (p.Y >= thumbY && p.Y <= thumbY + thumbHeight)
                    {
                        // 点击了滑块，记录偏移量并开始拖拽
                        _dragThumbOffsetY = p.Y - thumbY;
                        _isDraggingThumb = true;
                    }
                    else
                    {
                        // 点击了轨道（非滑块区域），滚动一页
                        if (p.Y < thumbY)
                            _scrollIndex = Math.Max(0, _scrollIndex - MAX_VISIBLE_ITEMS);
                        else
                            _scrollIndex = Math.Min(Items.Count - MAX_VISIBLE_ITEMS, _scrollIndex + MAX_VISIBLE_ITEMS);
                    }
                    return;
                }
            }

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
            // 处理滑块拖拽
            if (_isDraggingThumb)
            {
                if (Mouse.GetState().LeftButton == ButtonState.Released)
                {
                    // 鼠标松开，停止拖拽，抑制本次点击选择
                    _isDraggingThumb = false;
                    _suppressNextClick = true;
                }
                else
                {
                    // 根据鼠标位置更新滚动位置（使用偏移量保持滑块不跳动）
                    GetScrollBarGeometry(out int scrollBarX, out int scrollBarY, out int scrollBarHeight, out int thumbHeight, out int thumbY);
                    Point p = GetCursorPoint();
                    int targetThumbY = p.Y - _dragThumbOffsetY;
                    int maxThumbY = scrollBarHeight - thumbHeight;
                    if (maxThumbY > 0)
                    {
                        float ratio = (float)(targetThumbY - scrollBarY) / maxThumbY;
                        _scrollIndex = (int)(ratio * (Items.Count - MAX_VISIBLE_ITEMS));
                        _scrollIndex = Math.Max(0, Math.Min(_scrollIndex, Items.Count - MAX_VISIBLE_ITEMS));
                    }
                }
            }

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
                        
                        // 排除滚动条区域
                        if (p.X < Width - SCROLL_BAR_WIDTH)
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
            // 拖拽刚结束，抑制本次点击选择
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
                return;
            }

            // 如果正在拖拽滑块，停止拖拽但不关闭下拉框
            if (_isDraggingThumb)
            {
                _isDraggingThumb = false;
                return;
            }

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
            if (_isDraggingThumb)
                return; // 拖拽时不关闭下拉框
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

                int itemWidth = Width - 2 - SCROLL_BAR_WIDTH;

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
                GetScrollBarGeometry(out int scrollBarX, out int scrollBarY, out int scrollBarHeight, out int thumbHeight, out int thumbY);

                // 滚动条背景（轨道）
                DrawTexture(_scrollBarTexture, new Rectangle(scrollBarX, scrollBarY, SCROLL_BAR_WIDTH, scrollBarHeight), Color.Black * 0.5f);

                // 绘制滑块
                DrawTexture(_scrollBarTexture, new Rectangle(scrollBarX, thumbY, SCROLL_BAR_WIDTH, thumbHeight), Color.Gray);
            }
        }
    }
}
