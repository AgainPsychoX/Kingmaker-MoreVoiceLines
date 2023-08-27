using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace MoreVoiceLines
{
    internal class ConsoleWindowUtils
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOWMINIMIZED = 2;
        const int SW_SHOW = 5;

        public static void Hide()
        {
            ShowWindow(GetConsoleWindow(), SW_HIDE);
        }
        public static void Show()
        {
            ShowWindow(GetConsoleWindow(), SW_SHOW);
        }
    }
}
