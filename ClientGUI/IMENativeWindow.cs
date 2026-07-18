
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace ClientGUI
{
    public enum CompositionAttributes
    {
        /// <summary>
        /// 用户正在输入的字符。
        /// IME尚未转换此字符。
        /// </summary>
        Input = 0x00,
        /// <summary>
        /// 用户选择并由IME转换的字符。
        /// </summary>
        TargetConverted = 0x01,
        /// <summary>
        /// IME已转换的字符。
        /// </summary>
        Converted = 0x02,
        /// <summary>
        /// 正在转换的字符。用户已选择此字符，但IME尚未转换。
        /// </summary>
        TargetNotConverted = 0x03,
        /// <summary>
        /// IME无法转换的错误字符。例如，IME无法组合某些辅音。
        /// </summary>
        InputError = 0x04,
        /// <summary>
        /// IME不再转换的字符。
        /// </summary>
        FixedConverted = 0x05,
    }

    /// <summary>
    /// 特殊事件参数类，存储IME发送的新字符。
    /// </summary>
    public class IMEResultEventArgs : EventArgs
    {

        internal IMEResultEventArgs(char result)
        {
            this.Result = result;
        }

        /// <summary>
        /// 结果字符
        /// </summary>
        public char Result { get; private set; }
    }

    /// <summary>
    /// 处理IME的原生窗口类。
    /// </summary>
    public sealed class IMENativeWindow : NativeWindow, IDisposable
    {

        private IMMCompositionString
            _compstr, _compclause, _compattr,
            _compread, _compreadclause, _compreadattr,
            _resstr, _resclause,
            _resread, _resreadclause;
        private IMMCompositionInt _compcurpos;
        private bool _disposed;
        //private bool _showIMEWin;
        private IntPtr _context;

        /// <summary>
        /// 获取IME是否应启用的状态
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// 组合字符串
        /// </summary>
        public string CompositionString { get { return _compstr.ToString(); } }

        /// <summary>
        /// 组合子句
        /// </summary>
        public string CompositionClause { get { return _compclause.ToString(); } }

        /// <summary>
        /// 组合字符串读取
        /// </summary>
        public string CompositionReadString { get { return _compread.ToString(); } }

        /// <summary>
        /// 组合子句读取
        /// </summary>
        public string CompositionReadClause { get { return _compreadclause.ToString(); } }

        /// <summary>
        /// 结果字符串
        /// </summary>
        public string ResultString { get { return _resstr.ToString(); } }

        /// <summary>
        /// 结果子句
        /// </summary>
        public string ResultClause { get { return _resclause.ToString(); } }

        /// <summary>
        /// 结果字符串读取
        /// </summary>
        public string ResultReadString { get { return _resread.ToString(); } }

        /// <summary>
        /// 结果子句读取
        /// </summary>
        public string ResultReadClause { get { return _resreadclause.ToString(); } }

        /// <summary>
        /// 组合的光标位置
        /// </summary>
        public int CompositionCursorPos { get { return _compcurpos.Value; } }

        /// <summary>
        /// 候选词数组
        /// </summary>
        public string[] Candidates { get; private set; }

        /// <summary>
        /// 当前页的第一个候选词索引
        /// </summary>
        public uint CandidatesPageStart { get; private set; }

        /// <summary>
        /// 每页显示的候选词数量
        /// </summary>
        public uint CandidatesPageSize { get; private set; }

        /// <summary>
        /// 选中的候选词索引
        /// </summary>
        public uint CandidatesSelection { get; private set; }

        /// <summary>
        /// 获取指定字符索引处的组合属性。
        /// </summary>
        /// <param name="index">字符索引</param>
        /// <returns>组合属性</returns>
        public CompositionAttributes GetCompositionAttr(int index)
        {
            return (CompositionAttributes)_compattr[index];
        }

        /// <summary>
        /// 获取指定字符索引处的组合读取属性。
        /// </summary>
        /// <param name="index">字符索引</param>
        /// <returns>组合属性</returns>
        public CompositionAttributes GetCompositionReadAttr(int index)
        {
            return (CompositionAttributes)_compreadattr[index];
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
        /// 构造函数，必须在窗口创建时调用。
        /// </summary>
        /// <param name="handle">窗口句柄</param>
        public IMENativeWindow(IntPtr handle)
        {
            this._context = IntPtr.Zero;
            this.Candidates = new string[0];
            this._compcurpos = new IMMCompositionInt(IMM.GCSCursorPos);
            this._compstr = new IMMCompositionString(IMM.GCSCompStr);
            this._compclause = new IMMCompositionString(IMM.GCSCompClause);
            this._compattr = new IMMCompositionString(IMM.GCSCompAttr);
            this._compread = new IMMCompositionString(IMM.GCSCompReadStr);
            this._compreadclause = new IMMCompositionString(IMM.GCSCompReadClause);
            this._compreadattr = new IMMCompositionString(IMM.GCSCompReadAttr);
            this._resstr = new IMMCompositionString(IMM.GCSResultStr);
            this._resclause = new IMMCompositionString(IMM.GCSResultClause);
            this._resread = new IMMCompositionString(IMM.GCSResultReadStr);
            this._resreadclause = new IMMCompositionString(IMM.GCSResultReadClause);
            //this._showIMEWin = showDefaultIMEWindow;
            AssignHandle(handle);
        }

        /// <summary>
        /// 启用IME
        /// </summary>
        public void EnableIME()
        {
            IsEnabled = true;

            if (_context != IntPtr.Zero)
            {
                IMM.ImmAssociateContext(Handle, _context);
                IMM.ImmReleaseContext(Handle, _context);
                //        IMM.ShowReadingWindow(Handle, true);
                return;
            }

            // 修复全屏模式下 _context 为0的bug。
            ImeContext.Enable(Handle);
        }

        /// <summary>
        /// 禁用IME
        /// </summary>
        public void DisableIME()
        {
            IsEnabled = false;

            IMM.ImmAssociateContext(Handle, IntPtr.Zero);
            IMM.ImmReleaseContext(Handle, _context);
        }

        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                ReleaseHandle();
                _disposed = true;
            }
        }

        protected override void WndProc(ref Message msg)
        {
            switch (msg.Msg)
            {
                case IMM.ImeSetContext:
                    IMESetContext(ref msg);
                    break;
                case IMM.InputLanguageChange:
                    return;
                case IMM.ImeNotify:
                    IMENotify(msg.WParam.ToInt32());
                    // if (!_showIMEWin)
                    return;
                    break;
                case IMM.ImeStartCompostition:
                    IMEStartComposion(msg.LParam.ToInt32());
                    return;
                case IMM.ImeComposition:
                    IMEComposition(msg.LParam.ToInt32());
                    break;
                case IMM.ImeEndComposition:
                    IMEEndComposition(msg.LParam.ToInt32());
                    //  if (!_showIMEWin)
                    return;
                    break;
                case IMM.Char:
                    CharEvent(msg.WParam.ToInt32());
                    break;
            }
            base.WndProc(ref msg);
        }

        private void ClearComposition()
        {
            _compstr.Clear();
            _compclause.Clear();
            _compattr.Clear();
            _compread.Clear();
            _compreadclause.Clear();
            _compreadattr.Clear();
        }

        private void ClearResult()
        {
            _resstr.Clear();
            _resclause.Clear();
            _resread.Clear();
            _resreadclause.Clear();
        }

        #region IME消息处理

        private void IMESetContext(ref Message msg)
        {
            if (msg.WParam.ToInt32() == 1)
            {
                IntPtr ptr = IMM.ImmGetContext(Handle);
                if (_context == IntPtr.Zero)
                    _context = ptr;
                else if (ptr == IntPtr.Zero && IsEnabled)
                    EnableIME();

                _compcurpos.IMEHandle = _context;
                _compstr.IMEHandle = _context;
                _compclause.IMEHandle = _context;
                _compattr.IMEHandle = _context;
                _compread.IMEHandle = _context;
                _compreadclause.IMEHandle = _context;
                _compreadattr.IMEHandle = _context;
                _resstr.IMEHandle = _context;
                _resclause.IMEHandle = _context;
                _resread.IMEHandle = _context;
                _resreadclause.IMEHandle = _context;

                // if (!_showIMEWin)
                //    msg.LParam = (IntPtr)0;
            }
        }

        private void IMENotify(int WParam)
        {
            switch (WParam)
            {
                case IMM.ImnOpenCandidate:
                case IMM.ImnChangeCandidate:
                    IMEChangeCandidate();
                    break;
                case IMM.ImnCloseCandidate:
                    IMECloseCandidate();
                    break;
                case IMM.ImnPrivate:
                    break;
                default:
                    break;
            }
        }

        private void IMEChangeCandidate()
        {
            uint length = IMM.ImmGetCandidateList(_context, 0, IntPtr.Zero, 0);
            if (length > 0)
            {
                IntPtr pointer = Marshal.AllocHGlobal((int)length);
                length = IMM.ImmGetCandidateList(_context, 0, pointer, length);
                IMM.CandidateList cList = (IMM.CandidateList)Marshal.PtrToStructure(pointer, typeof(IMM.CandidateList));
                //IMM.CandidateList cList = IMM.GetCandidateList(_context, 0);
                CandidatesSelection = cList.dwSelection;
                CandidatesPageStart = cList.dwPageStart;
                CandidatesPageSize = cList.dwPageSize;

                //修复中文输入问题
                //if (cList.dwCount > 1)
                //{
                //    Candidates = new string[cList.dwCount];
                //    for (int i = 0; i < cList.dwCount; i++)
                //    {
                //        int sOffset = Marshal.ReadInt32(pointer, 24 + 4 * i);
                //        Candidates[i] = Marshal.PtrToStringUni((IntPtr)(pointer.ToInt32() + sOffset));
                //    }

                //    if (CandidatesReceived != null)
                //        CandidatesReceived(this, EventArgs.Empty);
                //}
                //else
                IMECloseCandidate();

                Marshal.FreeHGlobal(pointer);
            }
        }

        private void IMECloseCandidate()
        {
            CandidatesSelection = CandidatesPageStart = CandidatesPageSize = 0;
            Candidates = new string[0];

            if (CandidatesReceived != null)
                CandidatesReceived(this, EventArgs.Empty);
        }

        private void IMEStartComposion(int lParam)
        {
            ClearComposition();
            ClearResult();

            if (CompositionReceived != null)
                CompositionReceived(this, EventArgs.Empty);
        }

        private void IMEComposition(int lParam)
        {
            if (_compstr.Update(lParam))
            {
                _compclause.Update();
                _compattr.Update();
                _compread.Update();
                _compreadclause.Update();
                _compreadattr.Update();
                _compcurpos.Update();

                if (CompositionReceived != null)
                    CompositionReceived(this, EventArgs.Empty);
            }
        }

        private void IMEEndComposition(int lParam)
        {
            ClearComposition();

            if (_resstr.Update(lParam))
            {
                _resclause.Update();
                _resread.Update();
                _resreadclause.Update();
            }

            if (CompositionReceived != null)
                CompositionReceived(this, EventArgs.Empty);
        }

        private void CharEvent(int wParam)
        {
            if (ResultReceived != null)
                ResultReceived(this, new IMEResultEventArgs((char)wParam));

            if (IsEnabled)
                IMECloseCandidate();
        }

        #endregion
    }
}
