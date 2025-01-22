using Vampire.ReDta.Login;

namespace ReDta.Login.Test;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        LoginWin win = new LoginWin();
        win.ShowDialog();
        
    }
}
