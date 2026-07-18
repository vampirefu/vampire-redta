using ClientCore;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace ClientGUI
{
    public class INItializableWindow : XNAPanel
    {
        public INItializableWindow(WindowManager windowManager) : base(windowManager)
        {
        }

        protected CCIniFile ConfigIni { get; private set; }

        private bool hasCloseButton = false;
        private bool _initialized = false;

        /// <summary>
        /// 如果不为null，客户端将读取此名称的INI文件，而非窗口名称。
        /// </summary>
        protected string IniNameOverride { get; set; }

        public T FindChild<T>(string childName, bool optional = false) where T : XNAControl
        {
            T child = FindChild<T>(Children, childName);
            if (child == null && !optional)
                throw new KeyNotFoundException("Could not find required child control: " + childName);

            return child;
        }

        private T FindChild<T>(IEnumerable<XNAControl> list, string controlName) where T : XNAControl
        {
            foreach (XNAControl child in list)
            {
                if (child.Name == controlName)
                    return (T)child;

                T childOfChild = FindChild<T>(child.Children, controlName);
                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }

        /// <summary>
        /// 尝试定位当前控件的INI配置文件。
        /// 仅在配置路径存在时返回。
        /// </summary>
        /// <returns>INI配置文件路径</returns>
        protected string GetConfigPath()
        {
            string iniFileName = string.IsNullOrWhiteSpace(IniNameOverride) ? Name : IniNameOverride;

            // 获取主题特定路径
            FileInfo configIniPath = SafePath.GetFile(ProgramConstants.GetResourcePath(), FormattableString.Invariant($"{iniFileName}.ini"));
            if (configIniPath.Exists)
                return configIniPath.FullName;

            // 获取基础路径
            configIniPath = SafePath.GetFile(ProgramConstants.GetBaseResourcePath(), FormattableString.Invariant($"{iniFileName}.ini"));
            if (configIniPath.Exists)
                return configIniPath.FullName;

            if (iniFileName == Name)
                return null; // IniNameOverride 必须为null，无需继续

            iniFileName = Name;

            // 获取主题特定路径
            configIniPath = SafePath.GetFile(ProgramConstants.GetResourcePath(), FormattableString.Invariant($"{iniFileName}.ini"));
            if (configIniPath.Exists)
                return configIniPath.FullName;

            // 获取基础路径
            configIniPath = SafePath.GetFile(ProgramConstants.GetBaseResourcePath(), FormattableString.Invariant($"{iniFileName}.ini"));
            return configIniPath.Exists ? configIniPath.FullName : null;
        }

        public override void Initialize()
        {
           

            if (_initialized)
                throw new InvalidOperationException("INItializableWindow cannot be initialized twice.");

            string configIniPath = GetConfigPath();

            if (string.IsNullOrEmpty(configIniPath))
            {
                base.Initialize();
                return;
            }

            ConfigIni = new CCIniFile(configIniPath);

            if (Parser.Instance == null)
                new Parser(WindowManager);

            Parser.Instance.SetPrimaryControl(this);
            ReadINIForControl(this);
            ReadLateAttributesForControl(this);

            ParseExtraControls();

            base.Initialize();

            _initialized = true;
        }

        private void ParseExtraControls()
        {
            var section = ConfigIni.GetSection("$ExtraControls");

            if (section == null)
                return;

            foreach (var kvp in section.Keys)
            {
                if (!kvp.Key.StartsWith("$CC"))
                    continue;

                string[] parts = kvp.Value.Split(':');
                if (parts.Length != 2)
                    throw new ClientConfigurationException("Invalid $ExtraControl specified in " + Name + ": " + kvp.Value);

                if (!Children.Any(child => child.Name == parts[0]))
                {
                    var control = CreateChildControl(this, kvp.Value);
                    control.Name = parts[0];
                    control.DrawOrder = -Children.Count;
                    ReadINIForControl(control);
                }
            }
        }

        private void ReadINIRecursive(XNAControl control)
        {
            ReadINIForControl(control);

            foreach (var child in control.Children)
                ReadINIRecursive(child);
        }

        public override void ParseAttributeFromINI(IniFile iniFile, string key, string value)
        {
            if (key == "HasCloseButton")
                hasCloseButton = iniFile.GetBooleanValue(Name, key, hasCloseButton);

            base.ParseAttributeFromINI(iniFile, key, value);
        }

        protected void ReadINIForControl(XNAControl control)
        {
            var section = ConfigIni.GetSection(control.Name);
            if (section == null)
                return;

            Parser.Instance.SetPrimaryControl(this);

            foreach (var kvp in section.Keys)
            {
                if (kvp.Key.StartsWith("$CC"))
                {
                    var child = CreateChildControl(control, kvp.Value);
                    
                    ReadINIForControl(child);
                    child.Initialize();
                }

                //指定父组件
                else if (kvp.Key == "$Parent") {

                   
                    control.Parent.RemoveChild(control);
      
                    FindChild<XNAPanel>(kvp.Value).AddChild(control);

                }

                else if (kvp.Key == "$X")
                {
                    control.X = Parser.Instance.GetExprValue(kvp.Value, control);
                }
                else if (kvp.Key == "$Y")
                {
                    control.Y = Parser.Instance.GetExprValue(kvp.Value, control);
                }
                else if (kvp.Key == "$Width")
                {
                    control.Width = Parser.Instance.GetExprValue(kvp.Value, control);
                }
                else if (kvp.Key == "$Height")
                {
                    control.Height = Parser.Instance.GetExprValue(kvp.Value, control);
                }
                else if (kvp.Key == "$TextAnchor" && control is XNALabel)
                {
                    // TODO 重构这些使其更加面向对象
                    ((XNALabel)control).TextAnchor = (LabelTextAnchorInfo)Enum.Parse(typeof(LabelTextAnchorInfo), kvp.Value);
                }
             
                else if (kvp.Key == "$AnchorPoint" && control is XNALabel)
                {
                    string[] parts = kvp.Value.Split(',');
                    if (parts.Length != 2)
                        throw new FormatException("Invalid format for AnchorPoint: " + kvp.Value);
                    ((XNALabel)control).AnchorPoint = new Vector2(Parser.Instance.GetExprValue(parts[0], control), Parser.Instance.GetExprValue(parts[1], control));
                }
                else if (kvp.Key == "$LeftClickAction")
                {
                    if (kvp.Value == "Disable")
                        control.LeftClick += (s, e) => Disable();
                }
                else if (kvp.Key == "$Text")
                {
                    control.Text = section.GetStringValue(nameof(control.Text), string.Empty);
                }
                else
                {
                    control.ParseAttributeFromINI(ConfigIni, kvp.Key, kvp.Value);
                }
               
            }
        }

        /// <summary>
        /// 读取控件子控件的第二组属性。
        /// 允许将控件链接到在其之后定义的控件。
        /// </summary>
        private void ReadLateAttributesForControl(XNAControl control)
        {
            var section = ConfigIni.GetSection(control.Name);
            if (section == null)
                return;

            var children = Children.ToList();
            foreach (var child in children)
            {
                // 此逻辑未来也应该对其他类型启用，
                // 但需要XNAUI中的更改
                if (!(child is XNATextBox))
                    continue;

                var childSection = ConfigIni.GetSection(child.Name);
                if (childSection == null)
                    continue;

                string nextControl = childSection.GetStringValue("NextControl", null);
                if (!string.IsNullOrWhiteSpace(nextControl))
                {
                    var otherChild = children.Find(c => c.Name == nextControl);
                    if (otherChild != null)
                        ((XNATextBox)child).NextControl = otherChild;
                }

                string previousControl = childSection.GetStringValue("PreviousControl", null);
                if (!string.IsNullOrWhiteSpace(previousControl))
                {
                    var otherChild = children.Find(c => c.Name == previousControl);
                    if (otherChild != null)
                        ((XNATextBox)child).PreviousControl = otherChild;
                }
            }
        }

        private XNAControl CreateChildControl(XNAControl parent, string keyValue)
        {
            string[] parts = keyValue.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                throw new INIConfigException("Invalid child control definition " + keyValue);

            string childName = parts[0];
            if (string.IsNullOrEmpty(childName))
                throw new INIConfigException("Empty name in child control definition for " + parent.Name);

            XNAControl childControl = ClientGUICreator.GetXnaControl(parts[1]);

            if (Array.Exists(childName.ToCharArray(), c => !char.IsLetterOrDigit(c) && c != '_'))
                throw new INIConfigException("Names of INItializableWindow child controls must consist of letters, digits and underscores only. Offending name: " + parts[0]);

            childControl.Name = childName;
            parent.AddChildWithoutInitialize(childControl);
            return childControl;
        }
    }
}
