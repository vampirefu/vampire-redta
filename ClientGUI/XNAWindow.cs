using ClientCore;
using Rampastring.Tools;
using System;
using System.Collections.Generic;
using Rampastring.XNAUI;

namespace ClientGUI
{
    /// <summary>
    /// 在游戏窗口内显示的子窗口。
    /// 支持从INI文件轻松读取子控件属性。
    /// </summary>
    public class XNAWindow : XNAWindowBase
    {
        private IMENativeWindow _nativeWnd;
        private const string GENERIC_WINDOW_INI = "GenericWindow.ini";
        private const string GENERIC_WINDOW_SECTION = "GenericWindow";
        private const string EXTRA_CONTROLS = "ExtraControls";

        public XNAWindow(WindowManager windowManager) : base(windowManager)
        {
            _nativeWnd = new IMENativeWindow(windowManager.GetWindowHandle());
            _nativeWnd.CandidatesReceived += (s, e) => { if (CandidatesReceived != null) CandidatesReceived(s, e); };
            _nativeWnd.CompositionReceived += (s, e) => { if (CompositionReceived != null) CompositionReceived(s, e); };
            _nativeWnd.ResultReceived += (s, e) => { if (ResultReceived != null) ResultReceived(s, e); };

            _nativeWnd.EnableIME();
        }

        /// <summary>
        /// 当候选词更新时调用
        /// </summary>
        public event EventHandler CandidatesReceived;

        /// <summary>
        /// 当组合字符串更新时调用
        /// </summary>
        public event EventHandler CompositionReceived;

        /// <summary>
        /// 当新的结果字符到来时调用
        /// </summary>
        public event EventHandler<IMEResultEventArgs> ResultReceived;



        /// <summary>
        /// 用于此窗口主题化的INI文件。
        /// </summary>
        protected IniFile ThemeIni { get; set; }

        public override float Alpha
        {
            get
            {
                return 1.0f;
            }
        }

        protected virtual void SetAttributesFromIni()
        {
            if (SafePath.GetFile(ProgramConstants.GetResourcePath(), FormattableString.Invariant($"{Name}.ini")).Exists)
                GetINIAttributes(new CCIniFile(SafePath.CombineFilePath(ProgramConstants.GetResourcePath(), FormattableString.Invariant($"{Name}.ini"))));
            else if (SafePath.GetFile(ProgramConstants.GetBaseResourcePath(), FormattableString.Invariant($"{Name}.ini")).Exists)
                GetINIAttributes(new CCIniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), FormattableString.Invariant($"{Name}.ini"))));
            else if (SafePath.GetFile(ProgramConstants.GetResourcePath(), GENERIC_WINDOW_INI).Exists)
                GetINIAttributes(new CCIniFile(SafePath.CombineFilePath(ProgramConstants.GetResourcePath(), GENERIC_WINDOW_INI)));
            else
                GetINIAttributes(new CCIniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), GENERIC_WINDOW_INI)));
        }

        /// <summary>
        /// 从INI文件读取此窗口的属性。
        /// </summary>
        protected virtual void GetINIAttributes(IniFile iniFile)
        {
            ThemeIni = iniFile;

            List<string> keys = iniFile.GetSectionKeys(Name);

            if (keys != null)
            {
                foreach (string key in keys)
                    ParseAttributeFromINI(iniFile, key, iniFile.GetStringValue(Name, key, String.Empty));
            }
            else
            {
                keys = iniFile.GetSectionKeys(GENERIC_WINDOW_SECTION);

                if (keys != null)
                {
                    foreach (string key in keys)
                        ParseAttributeFromINI(iniFile, key, iniFile.GetStringValue(GENERIC_WINDOW_SECTION, key, String.Empty));
                }
            }

            ParseExtraControls(iniFile, EXTRA_CONTROLS);
            ReadChildControlAttributes(iniFile);
        }

        public override void Initialize()
        {
            base.Initialize();

            SetAttributesFromIni();
        }
    }
}
