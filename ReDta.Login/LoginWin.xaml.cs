using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;
using MessageNotification;
using ReDta.Login.Helper;
using Vampire.ReDta.Login.Services;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace Vampire.ReDta.Login
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWin : Window
    {
        public bool IsLogin { get; set; }

        public LoginWin()
        {
            InitializeComponent();
            Loaded += LoginWin_Loaded;
        }

        private void LoginWin_Loaded(object sender, RoutedEventArgs e)
        {
            string segment = "Login";
            var account = IniHelper.ReadItem(segment, "Account").AsString("");
            if (string.IsNullOrEmpty(account))
                return;

            tbx_account.Text = account;

            bool isSavePassword = IniHelper.ReadItem(segment, "IsSavePassword").AsBool(false);
            if (!isSavePassword)
                return;

            ckx_savePassword.IsChecked = isSavePassword;

            string password = IniHelper.ReadItem(segment, "Password").AsString("");
            if (string.IsNullOrEmpty(password))
                return;

            tbx_password.Password = AESHelper.AesDecrypt(password);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WinMove(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbx_account.Text) || string.IsNullOrEmpty(tbx_password.Password))
            {
                MessageBox.Show("检测到空的账号或者密码");
                return;
            }

            ButtonProgressAssist.SetIsIndicatorVisible(sender as Button, true);
            (sender as Button).IsEnabled = false;
            (sender as Button).Opacity = 1;

            string email = tbx_account.Text;
            string password = tbx_password.Password;

            string msg;
            Task.Run(() =>
            {
                if (LoginService.Login(email, password, out msg))
                {

                    Dispatcher.BeginInvoke(() =>
                    {
                        tbk_loginInfo.Foreground = new SolidColorBrush(Colors.Green);
                        IsLogin = true;

                        string segment = "Login";
                        IniHelper.Write(segment, "Account", tbx_account.Text);
                        IniHelper.Write(segment, "IsSavePassword", ckx_savePassword.IsChecked);
                        IniHelper.Write(segment, "Password", AESHelper.AesEncrypt(tbx_password.Password));
                    });
                }
                else
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        tbk_loginInfo.Foreground = new SolidColorBrush(Colors.Red);
                        IsLogin = false;
                    });
                }

                Dispatcher.BeginInvoke(() =>
                {
                    tbk_loginInfo.Text = msg;
                    tbk_loginInfo.Visibility = Visibility.Visible;

                    ButtonProgressAssist.SetIsIndicatorVisible(sender as Button, false);
                    (sender as Button).IsEnabled = true;
                    if (IsLogin)
                        this.Close();
                });
            });
        }
    }
}
