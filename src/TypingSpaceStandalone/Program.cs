using System;
using System.Windows.Forms;

namespace WAHU.TypingSpaceStandalone
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TypingSpaceStandaloneForm());
        }
    }
}
