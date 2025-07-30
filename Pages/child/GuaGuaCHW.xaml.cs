using NavigationViewExample;
using NavigationViewExample.Pages;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace WPFtransformer.Pages.child
{
    /// <summary>
    /// GuaGua.xaml 的交互逻辑
    /// </summary>
    public partial class GuaGuaCHW : Window
    {
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Appdata", "data2.txt");
        string temptext = "";
        private const int WM_NCHITTEST = 0x0084;        //标题栏
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int ResizeBorder = 8; // 拖拽触发区域宽度
        public GuaGuaCHW()
        {
            InitializeComponent();
        }   
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(filePath))
            {
                // 假设文件内容类似："window_800_600_100_200"
                string content = File.ReadAllText(filePath);
                string[] parts = content
                    .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
                    Width = w;
                if (double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var h))
                    Height = h;
                if (double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var t))
                    Top = t;
                if (double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var l))
                    Left = l;
                temptext = parts[0];
            }
            else
            {
                File.WriteAllText(filePath, MainWindow.writeText);
                MessageBox.Show("Please reuse the execution.");
            }
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            GuaGua.PCHW = 0;
            string[] saveParts = new string[]
            {
                temptext,
                this.Width.ToString(CultureInfo.InvariantCulture),
                this.Height.ToString(CultureInfo.InvariantCulture),
                this.Top.ToString(CultureInfo.InvariantCulture),
                this.Left.ToString(CultureInfo.InvariantCulture)
            };
            string output = string.Join("_", saveParts);
            File.WriteAllText(filePath, output);
        }
        //标题栏
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            hwndSource.AddHook(WndProc);
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                handled = true;
                var pt = new Point((lParam.ToInt32() & 0xFFFF), (lParam.ToInt32() >> 16));
                var relative = PointFromScreen(pt);
                var w = ActualWidth; var h = ActualHeight;
                // 四角
                if (relative.X <= ResizeBorder && relative.Y <= ResizeBorder) return (IntPtr)HTTOPLEFT;
                if (relative.X >= w - ResizeBorder && relative.Y <= ResizeBorder) return (IntPtr)HTTOPRIGHT;
                if (relative.X <= ResizeBorder && relative.Y >= h - ResizeBorder) return (IntPtr)HTBOTTOMLEFT;
                if (relative.X >= w - ResizeBorder && relative.Y >= h - ResizeBorder) return (IntPtr)HTBOTTOMRIGHT;
                // 边缘
                if (relative.X <= ResizeBorder) return (IntPtr)HTLEFT;
                if (relative.X >= w - ResizeBorder) return (IntPtr)HTRIGHT;
                if (relative.Y <= ResizeBorder) return (IntPtr)HTTOP;
                if (relative.Y >= h - ResizeBorder) return (IntPtr)HTBOTTOM;
                return (IntPtr)HTCLIENT;
            }
            return IntPtr.Zero;
        }
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
        private void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaxRestore_Click(object s, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click(object s, RoutedEventArgs e) => Close();
    }
}
