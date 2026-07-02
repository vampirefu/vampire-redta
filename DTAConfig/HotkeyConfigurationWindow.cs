using ClientCore;
using ClientGUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;

namespace DTAConfig
{
    /// <summary>
    /// 用于配置游戏内快捷键的窗口。
    /// </summary>
    public class HotkeyConfigurationWindow : XNAWindow
    {
        private readonly string HOTKEY_TIP_TEXT = "按下键盘...";
        private const string HOTKEY_INI_SECTION = "Hotkey";
        private const string KEYBOARD_COMMANDS_INI = "KeyboardCommands.ini";

        public HotkeyConfigurationWindow(WindowManager windowManager) : base(windowManager)
        {
        }

        /// <summary>
        /// 客户端不允许用作常规快捷键的按键。
        /// </summary>
        private readonly Keys[] keyBlacklist = new Keys[]
        {
            Keys.LeftAlt,
            Keys.RightAlt,
            Keys.LeftControl,
            Keys.RightControl,
            Keys.LeftShift,
            Keys.RightShift
        };

        private List<GameCommand> gameCommands = new List<GameCommand>();

        private XNAClientDropDown ddCategory;
        private XNAMultiColumnListBox lbHotkeys;

        private XNAPanel hotkeyInfoPanel;
        private XNALabel lblCommandCaption;
        private XNALabel lblDescription;
        private XNALabel lblCurrentHotkeyValue;
        private XNALabel lblNewHotkeyValue;
        private XNALabel lblCurrentlyAssignedTo;

        private XNALabel lblDefaultHotkeyValue;
        private XNAClientButton btnResetKey;

        private IniFile keyboardINI;

        private Hotkey pendingHotkey;
        private KeyModifiers lastFrameModifiers;

        public override void Initialize()
        {
            ReadGameCommands();

            Name = "HotkeyConfigurationWindow";
            ClientRectangle = new Rectangle(0, 0, 600, 450);
            BackgroundTexture = AssetLoader.LoadTextureUncached("hotkeyconfigbg.png");

            var lblCategory = new XNALabel(WindowManager);
            lblCategory.Name = "lblCategory";
            lblCategory.ClientRectangle = new Rectangle(12, 12, 0, 0);
            lblCategory.Text = "类别:";

            ddCategory = new XNAClientDropDown(WindowManager);
            ddCategory.Name = "ddCategory";
            ddCategory.ClientRectangle = new Rectangle(lblCategory.Right + 12,
                lblCategory.Y - 1, 250, ddCategory.Height);

            HashSet<string> categories = new HashSet<string>();

            foreach (var command in gameCommands)
            {
                if (!categories.Contains(command.Category))
                    categories.Add(command.Category);
            }

            foreach (string category in categories)
                ddCategory.AddItem(category);

            lbHotkeys = new XNAMultiColumnListBox(WindowManager);
            lbHotkeys.Name = "lbHotkeys";
            lbHotkeys.ClientRectangle = new Rectangle(12, ddCategory.Bottom + 12,
                ddCategory.Right - 12, Height - ddCategory.Bottom - 59);
            lbHotkeys.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbHotkeys.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbHotkeys.AddColumn("命令", 150);
            lbHotkeys.AddColumn("快捷键", lbHotkeys.Width - 150);

            hotkeyInfoPanel = new XNAPanel(WindowManager);
            hotkeyInfoPanel.Name = "HotkeyInfoPanel";
            hotkeyInfoPanel.ClientRectangle = new Rectangle(lbHotkeys.Right + 12,
                ddCategory.Y, Width - lbHotkeys.Right - 24, lbHotkeys.Height + ddCategory.Height + 12);

            lblCommandCaption = new XNALabel(WindowManager);
            lblCommandCaption.Name = "lblCommandCaption";
            lblCommandCaption.FontIndex = 1;
            lblCommandCaption.ClientRectangle = new Rectangle(12, 12, 0, 0);
            lblCommandCaption.Text = "命令名称";

            lblDescription = new XNALabel(WindowManager);
            lblDescription.Name = "lblDescription";
            lblDescription.ClientRectangle = new Rectangle(12, lblCommandCaption.Bottom + 12, 0, 0);
            lblDescription.Text = "命令描述";

            var lblCurrentHotkey = new XNALabel(WindowManager);
            lblCurrentHotkey.Name = "lblCurrentHotkey";
            lblCurrentHotkey.ClientRectangle = new Rectangle(lblDescription.X,
                lblDescription.Bottom + 48, 0, 0);
            lblCurrentHotkey.FontIndex = 1;
            lblCurrentHotkey.Text = "当前绑定按键:";

            lblCurrentHotkeyValue = new XNALabel(WindowManager);
            lblCurrentHotkeyValue.Name = "lblCurrentHotkeyValue";
            lblCurrentHotkeyValue.ClientRectangle = new Rectangle(lblDescription.X,
                lblCurrentHotkey.Bottom + 6, 0, 0);
            lblCurrentHotkeyValue.Text = "当前热键值";

            var lblNewHotkey = new XNALabel(WindowManager);
            lblNewHotkey.Name = "lblNewHotkey";
            lblNewHotkey.ClientRectangle = new Rectangle(lblDescription.X,
                lblCurrentHotkeyValue.Bottom + 48, 0, 0);
            lblNewHotkey.FontIndex = 1;
            lblNewHotkey.Text = "新热键:";

            lblNewHotkeyValue = new XNALabel(WindowManager);
            lblNewHotkeyValue.Name = "lblNewHotkeyValue";
            lblNewHotkeyValue.ClientRectangle = new Rectangle(lblDescription.X,
                lblNewHotkey.Bottom + 6, 0, 0);
            lblNewHotkeyValue.Text = HOTKEY_TIP_TEXT;

            lblCurrentlyAssignedTo = new XNALabel(WindowManager);
            lblCurrentlyAssignedTo.Name = "lblCurrentlyAssignedTo";
            lblCurrentlyAssignedTo.ClientRectangle = new Rectangle(lblDescription.X,
                lblNewHotkeyValue.Bottom + 12, 0, 0);
            lblCurrentlyAssignedTo.Text = "当前已绑定至:" + "\nKey";

            var btnAssign = new XNAClientButton(WindowManager);
            btnAssign.Name = "btnAssign";
            btnAssign.ClientRectangle = new Rectangle(lblDescription.X,
                lblCurrentlyAssignedTo.Bottom + 24, UIDesignConstants.BUTTON_WIDTH_121, UIDesignConstants.BUTTON_HEIGHT);
            btnAssign.Text = "绑定热键";
            btnAssign.LeftClick += BtnAssign_LeftClick;

            btnResetKey = new XNAClientButton(WindowManager);
            btnResetKey.Name = "btnResetKey";
            btnResetKey.ClientRectangle = new Rectangle(btnAssign.X, btnAssign.Bottom + 12, btnAssign.Width, 23);
            btnResetKey.Text = "重设为默认";
            btnResetKey.LeftClick += BtnReset_LeftClick;

            var lblDefaultHotkey = new XNALabel(WindowManager);
            lblDefaultHotkey.Name = "lblOriginalHotkey";
            lblDefaultHotkey.ClientRectangle = new Rectangle(lblCurrentHotkey.X, btnResetKey.Bottom + 12, 0, 0);
            lblDefaultHotkey.Text = "默认热键:";

            lblDefaultHotkeyValue = new XNALabel(WindowManager);
            lblDefaultHotkeyValue.Name = "lblDefaultHotkeyValue";
            lblDefaultHotkeyValue.ClientRectangle = new Rectangle(lblDefaultHotkey.Right + 12, lblDefaultHotkey.Y, 0, 0);

            var btnSave = new XNAClientButton(WindowManager);
            btnSave.Name = "btnSave";
            btnSave.ClientRectangle = new Rectangle(12, lbHotkeys.Bottom + 12, UIDesignConstants.BUTTON_WIDTH_92, UIDesignConstants.BUTTON_HEIGHT);
            btnSave.Text = "保存";
            btnSave.LeftClick += BtnSave_LeftClick;

            var btnResetAllKeys = new XNAClientButton(WindowManager);
            btnResetAllKeys.Name = "btnResetAllToDefaults";
            btnResetAllKeys.ClientRectangle = new Rectangle(0, btnSave.Y, UIDesignConstants.BUTTON_WIDTH_121, UIDesignConstants.BUTTON_HEIGHT);
            btnResetAllKeys.Text = "重设全部热键";
            btnResetAllKeys.LeftClick += BtnResetToDefaults_LeftClick;
            AddChild(btnResetAllKeys);
            btnResetAllKeys.CenterOnParentHorizontally();

            var btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Name = "btnExit";
            btnCancel.ClientRectangle = new Rectangle(Width - 104, btnSave.Y, UIDesignConstants.BUTTON_WIDTH_92, UIDesignConstants.BUTTON_HEIGHT);
            btnCancel.Text = "取消";
            btnCancel.LeftClick += BtnCancel_LeftClick;

            AddChild(lbHotkeys);
            AddChild(lblCategory);
            AddChild(ddCategory);
            AddChild(hotkeyInfoPanel);
            AddChild(btnSave);
            AddChild(btnCancel);
            hotkeyInfoPanel.AddChild(lblCommandCaption);
            hotkeyInfoPanel.AddChild(lblDescription);
            hotkeyInfoPanel.AddChild(lblCurrentHotkey);
            hotkeyInfoPanel.AddChild(lblCurrentHotkeyValue);
            hotkeyInfoPanel.AddChild(lblNewHotkey);
            hotkeyInfoPanel.AddChild(lblNewHotkeyValue);
            hotkeyInfoPanel.AddChild(lblCurrentlyAssignedTo);
            hotkeyInfoPanel.AddChild(lblDefaultHotkey);
            hotkeyInfoPanel.AddChild(lblDefaultHotkeyValue);
            hotkeyInfoPanel.AddChild(btnAssign);
            hotkeyInfoPanel.AddChild(btnResetKey);

            if (categories.Count > 0)
            {
                hotkeyInfoPanel.Disable();
                lbHotkeys.SelectedIndexChanged += LbHotkeys_SelectedIndexChanged;

                ddCategory.SelectedIndexChanged += DdCategory_SelectedIndexChanged;
                ddCategory.SelectedIndex = 0;
            }
            else
                Logger.Log("No keyboard game commands exist!");

            GameProcessLogic.GameProcessExited += GameProcessLogic_GameProcessExited;

            base.Initialize();

            CenterOnParent();

            Keyboard.OnKeyPressed += Keyboard_OnKeyPressed;
            EnabledChanged += HotkeyConfigurationWindow_EnabledChanged;
        }

        /// <summary>
        /// 从INI文件中读取游戏命令。
        /// </summary>
        private void ReadGameCommands()
        {
            var gameCommandsIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), KEYBOARD_COMMANDS_INI));

            List<string> sections = gameCommandsIni.GetSections();

            foreach (string sectionName in sections)
            {
                gameCommands.Add(new GameCommand(gameCommandsIni.GetSection(sectionName)));
            }
        }

        /// <summary>
        /// 将当前选中游戏命令的快捷键重置为默认值。
        /// </summary>
        private void BtnReset_LeftClick(object sender, EventArgs e)
        {
            if (lbHotkeys.SelectedIndex < 0 || lbHotkeys.SelectedIndex >= lbHotkeys.ItemCount)
            {
                return;
            }

            var command = (GameCommand)lbHotkeys.GetItem(0, lbHotkeys.SelectedIndex).Tag;
            command.Hotkey = command.DefaultHotkey;

            // 如果快捷键已绑定到其他命令，则解除绑定
            foreach (var gameCommand in gameCommands)
            {
                if (pendingHotkey.Equals(gameCommand.Hotkey))
                    gameCommand.Hotkey = new Hotkey(Keys.None, KeyModifiers.None);
            }

            pendingHotkey = new Hotkey(Keys.None, KeyModifiers.None);
            RefreshHotkeyList();
        }

        private void BtnResetToDefaults_LeftClick(object sender, EventArgs e)
        {
            foreach (var command in gameCommands)
            {
                command.Hotkey = command.DefaultHotkey;
            }

            RefreshHotkeyList();
        }

        private void HotkeyConfigurationWindow_EnabledChanged(object sender, EventArgs e)
        {
            if (Enabled)
            {
                LoadKeyboardINI();
                RefreshHotkeyList();
            }
        }

        /// <summary>
        /// 当游戏进程退出时重新加载Keyboard.ini。
        /// </summary>
        private void GameProcessLogic_GameProcessExited()
        {
            WindowManager.AddCallback(new Action(LoadKeyboardINI), null);
        }

        private void LoadKeyboardINI()
        {
            keyboardINI = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, ClientConfiguration.Instance.KeyboardINI));

            if (SafePath.GetFile(ProgramConstants.GamePath, ClientConfiguration.Instance.KeyboardINI).Exists)
            {
                foreach (var command in gameCommands)
                {
                    int hotkey = keyboardINI.GetIntValue("Hotkey", command.ININame, 0);

                    Hotkey hotkeyStruct = new Hotkey(hotkey);
                    command.Hotkey = new Hotkey(GetKeyOverride(hotkeyStruct.Key), hotkeyStruct.Modifier);
                }
            }
            else
            {
                foreach (var command in gameCommands)
                {
                    command.Hotkey = command.DefaultHotkey;
                }
            }
        }

        private void LbHotkeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbHotkeys.SelectedIndex < 0 || lbHotkeys.SelectedIndex >= lbHotkeys.ItemCount)
            {
                hotkeyInfoPanel.Disable();
                return;
            }

            hotkeyInfoPanel.Enable();
            var command = (GameCommand)lbHotkeys.GetItem(0, lbHotkeys.SelectedIndex).Tag;
            lblCommandCaption.Text = command.UIName;
            lblDescription.Text = Renderer.FixText(command.Description, lblDescription.FontIndex,
                hotkeyInfoPanel.Width - lblDescription.X).Text;
            lblCurrentHotkeyValue.Text = command.Hotkey.ToStringWithNone();

            lblDefaultHotkeyValue.Text = command.DefaultHotkey.ToStringWithNone();
            btnResetKey.Enabled = !command.Hotkey.Equals(command.DefaultHotkey);

            lblNewHotkeyValue.Text = HOTKEY_TIP_TEXT;
            pendingHotkey = new Hotkey(Keys.None, KeyModifiers.None);
            lblCurrentlyAssignedTo.Text = string.Empty;
        }

        private void DdCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbHotkeys.ClearItems();
            lbHotkeys.TopIndex = 0;
            string category = ddCategory.SelectedItem.Text;
            foreach (var command in gameCommands)
            {
                if (command.Category == category)
                {
                    lbHotkeys.AddItem(new XNAListBoxItem[] {
                        new XNAListBoxItem() { Text = command.UIName, Tag = command },
                        new XNAListBoxItem() { Text = command.Hotkey.ToString() }
                    });
                }
            }

            lbHotkeys.SelectedIndex = -1;
        }

        private void BtnAssign_LeftClick(object sender, EventArgs e)
        {
            if (lbHotkeys.SelectedIndex < 0 || lbHotkeys.SelectedIndex >= lbHotkeys.ItemCount)
            {
                return;
            }

            // 如果快捷键已绑定到其他命令，则解除绑定
            foreach (var gameCommand in gameCommands)
            {
                if (pendingHotkey.Equals(gameCommand.Hotkey))
                    gameCommand.Hotkey = new Hotkey(Keys.None, KeyModifiers.None);
            }

            var command = (GameCommand)lbHotkeys.GetItem(0, lbHotkeys.SelectedIndex).Tag;
            command.Hotkey = pendingHotkey;
            RefreshHotkeyList();
            pendingHotkey = new Hotkey(Keys.None, KeyModifiers.None);
        }

        private void RefreshHotkeyList()
        {
            int selectedIndex = lbHotkeys.SelectedIndex;
            int topIndex = lbHotkeys.TopIndex;
            DdCategory_SelectedIndexChanged(null, EventArgs.Empty);
            lbHotkeys.TopIndex = topIndex;
            lbHotkeys.SelectedIndex = selectedIndex;
        }

        /// <summary>
        /// 检测用户是否按下按键以生成新快捷键。
        /// </summary>
        private void Keyboard_OnKeyPressed(object sender, Rampastring.XNAUI.Input.KeyPressEventArgs e)
        {
            foreach (var blacklistedKey in keyBlacklist)
            {
                if (e.PressedKey == blacklistedKey)
                    return;
            }

            var currentModifiers = GetCurrentModifiers();

            // XNA按键似乎与Windows虚拟键码匹配！这省了我们一些工作
            pendingHotkey = new Hotkey(GetKeyOverride(e.PressedKey), currentModifiers);

            lblCurrentlyAssignedTo.Text = string.Empty;

            foreach (var command in gameCommands)
            {
                if (pendingHotkey.Equals(command.Hotkey))
                    lblCurrentlyAssignedTo.Text = "当前已绑定至:" + Environment.NewLine + command.UIName;
            }
        }

        private void BtnCancel_LeftClick(object sender, EventArgs e)
        {
            Disable();
        }

        private void BtnSave_LeftClick(object sender, EventArgs e)
        {
            WriteKeyboardINI();
            Disable();
        }

        /// <summary>
        /// 更新窗口逻辑。
        /// 用于保持"新快捷键"显示与键盘修饰键同步。
        /// </summary>
        /// <param name="gameTime">提供计时值的快照。</param>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var oldModifiers = pendingHotkey.Modifier;
            var currentModifiers = GetCurrentModifiers();

            if ((pendingHotkey.Key == Keys.None && currentModifiers != oldModifiers)
                ||
                (pendingHotkey.Key != Keys.None &&
                lastFrameModifiers == KeyModifiers.None &&
                currentModifiers != lastFrameModifiers))
            {
                pendingHotkey = new Hotkey(Keys.None, currentModifiers);
                lblCurrentlyAssignedTo.Text = string.Empty;
            }

            string displayString = pendingHotkey.ToString();
            if (displayString != string.Empty)
                lblNewHotkeyValue.Text = pendingHotkey.ToString();
            else
                lblNewHotkeyValue.Text = HOTKEY_TIP_TEXT;

            lastFrameModifiers = currentModifiers;
        }

        /// <summary>
        /// 检测用户当前按下的修饰键（Ctrl、Shift、Alt）。
        /// </summary>
        private KeyModifiers GetCurrentModifiers()
        {
            var currentModifiers = KeyModifiers.None;

            if (Keyboard.IsKeyHeldDown(Keys.RightControl) ||
                Keyboard.IsKeyHeldDown(Keys.LeftControl))
            {
                currentModifiers |= KeyModifiers.Ctrl;
            }

            if (Keyboard.IsKeyHeldDown(Keys.RightShift) ||
                Keyboard.IsKeyHeldDown(Keys.LeftShift))
            {
                currentModifiers |= KeyModifiers.Shift;
            }

            if (Keyboard.IsKeyHeldDown(Keys.LeftAlt) ||
                Keyboard.IsKeyHeldDown(Keys.RightAlt))
            {
                currentModifiers |= KeyModifiers.Alt;
            }

            return currentModifiers;
        }

        private void WriteKeyboardINI()
        {
            var keyboardIni = new IniFile();
            foreach (var command in gameCommands)
            {
                keyboardIni.SetStringValue("Hotkey", command.ININame, command.Hotkey.GetTSEncoded().ToString());
            }

            keyboardIni.WriteIniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, ClientConfiguration.Instance.KeyboardINI));
        }

        /// <summary>
        /// 允许定义在游戏内用途上与其他按键匹配的按键，
        /// 并应显示为那些按键。
        /// </summary>
        /// <param name="key">按键。</param>
        private Keys GetKeyOverride(Keys key)
        {
            // 12在游戏中实际上是NumPad5
            if (key == (Keys)12)
                return Keys.NumPad5;

            return key;
        }

        /// <summary>
        /// 可以绑定到键盘按键的游戏命令。
        /// </summary>
        class GameCommand
        {
            public GameCommand(string uiName, string category, string description, string iniName)
            {
                UIName = uiName;
                Category = category;
                Description = description;
                ININame = iniName;
            }

            /// <summary>
            /// 创建游戏命令并从INI节中解析其信息。
            /// </summary>
            /// <param name="iniSection">INI节。</param>
            public GameCommand(IniSection iniSection)
            {
                ININame = iniSection.SectionName;
                UIName = iniSection.GetStringValue("UIName", "未命名命令");
                Category = iniSection.GetStringValue("Category", "未知类别");
                Description = iniSection.GetStringValue("Description", "未知描述");
                DefaultHotkey = new Hotkey(iniSection.GetIntValue("DefaultKey", 0));
            }

            /// <summary>
            /// 将游戏命令信息写入INI文件。
            /// </summary>
            /// <param name="iniFile">INI文件。</param>
            public void WriteToIni(IniFile iniFile)
            {
                var section = new IniSection(ININame);
                section.SetStringValue("UIName", UIName);
                section.SetStringValue("Category", Category);
                section.SetStringValue("Description", Description);
                section.SetIntValue("DefaultKey", DefaultHotkey.GetTSEncoded());
                iniFile.AddSection(section);
            }

            public string UIName { get; private set; }
            public string Category { get; private set; }
            public string Description { get; private set; }
            public string ININame { get; private set; }
            public Hotkey Hotkey { get; set; }
            public Hotkey DefaultHotkey { get; private set; }
        }

        [Flags]
        private enum KeyModifiers
        {
            None = 0,
            Shift = 1,
            Ctrl = 2,
            Alt = 4
        }

        /// <summary>
        /// 表示带修饰键的键盘按键。
        /// </summary>
        struct Hotkey
        {
            /// <summary>
            /// 通过解码Tiberian Sun / Red Alert 2编码的键值创建新快捷键。
            /// </summary>
            /// <param name="encodedKeyValue">编码的键值。</param>
            public Hotkey(int encodedKeyValue)
            {
                Key = (Keys)(encodedKeyValue & 255);
                Modifier = (KeyModifiers)(encodedKeyValue >> 8);
            }

            public Hotkey(Keys key, KeyModifiers modifiers)
            {
                Key = key;
                Modifier = modifiers;
            }

            public Keys Key { get; private set; }
            public KeyModifiers Modifier { get; private set; }

            public override string ToString()
            {
                if (Key == Keys.None && Modifier == KeyModifiers.None)
                    return string.Empty;

                return GetString();
            }

            public string ToStringWithNone()
            {
                if (Key == Keys.None && Modifier == KeyModifiers.None)
                    return "无";

                return GetString();
            }

            /// <summary>
            /// 创建此按键的显示字符串。
            /// </summary>
            private string GetString()
            {
                string str = "";

                if (Modifier.HasFlag(KeyModifiers.Shift))
                    str += "SHIFT+";

                if (Modifier.HasFlag(KeyModifiers.Ctrl))
                    str += "CTRL+";

                if (Modifier.HasFlag(KeyModifiers.Alt))
                    str += "ALT+";

                if (Key == Keys.None)
                    return str;

                return str + GetKeyDisplayString(Key);
            }

            /// <summary>
            /// 以Tiberian Sun / Red Alert 2 Keyboard.ini编码格式返回快捷键。
            /// </summary>
            public int GetTSEncoded()
            {
                return ((int)Modifier << 8) + (int)Key;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Hotkey))
                    return false;

                var hotkey = (Hotkey)obj;
                return hotkey.Key == Key && hotkey.Modifier == Modifier;
            }

            public override int GetHashCode()
            {
                return GetTSEncoded();
            }

            /// <summary>
            /// 返回XNA按键的显示字符串。
            /// 允许覆盖特定按键枚举名称，使其更适合UI显示。
            /// </summary>
            /// <param name="key">按键。</param>
            /// <returns>字符串。</returns>
            private string GetKeyDisplayString(Keys key)
            {
                switch (key)
                {
                    case Keys.D0:
                        return "0";
                    case Keys.D1:
                        return "1";
                    case Keys.D2:
                        return "2";
                    case Keys.D3:
                        return "3";
                    case Keys.D4:
                        return "4";
                    case Keys.D5:
                        return "5";
                    case Keys.D6:
                        return "6";
                    case Keys.D7:
                        return "7";
                    case Keys.D8:
                        return "8";
                    case Keys.D9:
                        return "9";
                    default:
                        return key.ToString();
                }
            }
        }
    }
}
