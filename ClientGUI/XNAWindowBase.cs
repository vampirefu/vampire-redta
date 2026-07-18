using ClientCore;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System.Linq;

namespace ClientGUI
{
    public class XNAWindowBase : XNAPanel
    {
        public XNAWindowBase(WindowManager windowManager) : base(windowManager)
        {
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.TILED;
        }

        /// <summary>
        /// 从INI文件的特定节读取额外控件信息。
        /// </summary>
        /// <param name="iniFile">INI文件。</param>
        /// <param name="sectionName">节名称。</param>
        protected virtual void ParseExtraControls(IniFile iniFile, string sectionName)
        {
            var section = iniFile.GetSection(sectionName);

            if (section == null)
                return;

            foreach (var kvp in section.Keys)
            {
                string[] parts = kvp.Value.Split(':');
                if (parts.Length != 2)
                    throw new ClientConfigurationException("Invalid ExtraControl specified in " + Name + ": " + kvp.Value);

                if (!Children.Any(child => child.Name == parts[0]))
                {
                    XNAControl control = ClientGUICreator.GetXnaControl(parts[1]);
                    control.Name = parts[0];
                    control.DrawOrder = -Children.Count;
                    AddChild(control);
                }
            }
        }

        protected virtual void ReadChildControlAttributes(IniFile iniFile)
        {
            foreach (XNAControl child in Children)
            {
                if (!(typeof(XNAWindowBase).IsAssignableFrom(child.GetType())))
                    child.GetAttributes(iniFile);
            }
        }

        /// <summary>
        /// 使用指定的GUI创建器和控件类型名称创建具有给定名称的控件。
        /// </summary>
        /// <param name="guiCreator">要使用的 <see cref="GUICreator"/>。</param>
        /// <param name="controlTypeName">控件类型的名称。</param>
        /// <param name="controlName">创建的控件名称。</param>
        /// <returns>创建的控件。</returns>
        protected virtual XNAControl CreateControl(GUICreator guiCreator, string controlTypeName, string controlName)
        {
            var control = guiCreator.CreateControl(WindowManager, controlTypeName);
            control.Name = controlName;
            control.DrawOrder = -Children.Count;
            AddChild(control);
            return control;
        }
    }
}
