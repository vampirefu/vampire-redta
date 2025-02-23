using ReDta.DxMainClient.Extend.Components;
using Vampire.ReDta.Login;

namespace ReDta.Login.Test;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        //LoginWin win = new LoginWin();

        UpdatePromptWin win = new UpdatePromptWin();

        win.ShowDialog();

    }
}
