using Microsoft.Xna.Framework.Input;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;

namespace ClientGUI
{
    /// <summary>
    /// 存储已输入消息的文本框，允许通过方向键查看历史消息。
    /// </summary>
    public class XNAChatTextBox : XNASuggestionTextBox
    {
        public XNAChatTextBox(WindowManager windowManager) : base(windowManager)
        {
            EnterPressed += XNAChatTextBox_EnterPressed;
        }

        private LinkedList<string> enteredMessages = new LinkedList<string>();
        private LinkedListNode<string> currentNode;

        private void XNAChatTextBox_EnterPressed(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(Text))
                enteredMessages.AddFirst(Text);
        }

        protected override bool HandleKeyPress(Keys key)
        {
            if (key == Keys.Up)
            {
                if (currentNode == null)
                {
                    if (enteredMessages.First != null)
                        currentNode = enteredMessages.First;
                }
                else
                {
                    if (currentNode.Next != null)
                        currentNode = currentNode.Next;
                }

                if (currentNode != null)
                    Text = currentNode.Value;

                return true;
            }

            if (key == Keys.Down)
            {
                if (currentNode != null && currentNode.Previous != null)
                {
                    currentNode = currentNode.Previous;
                    Text = currentNode.Value;
                }

                return true;
            }

            currentNode = null;
            return base.HandleKeyPress(key);
        }
    }
}
