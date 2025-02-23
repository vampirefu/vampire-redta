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
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace ReDta.DxMainClient.Extend.Components
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdatePromptWin : Window
    {
        public bool IsLogin { get; set; }

        public UpdatePromptWin()
        {
            InitializeComponent();
        }

        public void SetVersion(string version)
        {
            tbx_version.Text = version;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WinMove(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
