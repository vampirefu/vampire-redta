using ClientGUI;
using Rampastring.XNAUI;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    /// <summary>
    /// 如果在点击时没有子控件获得输入焦点则隐藏自身的面板。
    /// </summary>
    public class PrivateMessagingPanel : DarkeningPanel
    {
        public PrivateMessagingPanel(WindowManager windowManager) : base(windowManager)
        {
        }

        public override void OnLeftClick()
        {
            bool hideControl = true;

            foreach (var child in Children)
            {
                if (child.IsActive)
                {
                    hideControl = false;
                    break;
                }
            }

            if (hideControl)
                Hide();

            base.OnLeftClick();
        }


    }
}
