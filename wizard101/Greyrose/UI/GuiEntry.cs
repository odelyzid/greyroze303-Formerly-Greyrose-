using System.Windows.Forms;

namespace Greyrose.UI
{
    static class GuiEntry
    {
        public static void Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
