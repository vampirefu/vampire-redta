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
using Vampire.ReDta.Login.Services;

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
            string email = tbx_account.Text;
            string password = tbx_password.Password;

            string msg;
            if (LoginService.Login(email, password, out msg))
            {
                tbk_loginInfo.Foreground = new SolidColorBrush(Colors.Green);
                IsLogin = true;
            }
            else
            {
                tbk_loginInfo.Foreground = new SolidColorBrush(Colors.Red);
                IsLogin = false;
            }
            tbk_loginInfo.Text = msg;
            tbk_loginInfo.Visibility = Visibility.Visible;

            if (IsLogin)
                this.Close();
        }
    }
}
