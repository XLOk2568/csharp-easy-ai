using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using XLOKProject;
using static NVTemperatureControl.MainWindow;
using static System.Net.WebRequestMethods;

using File=System.IO.File;
using Path=System.IO.Path;

namespace NVTemperatureControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HomePage2? homePage2_;
        private int homepage2_Is = 0;
        private AifuckGame? aifuckGame_;
        private int aifuckGame_Is = 0;
        private Crosshair? crosshair_;
        private int crosshair_Is = 0;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (homepage2_Is == 0)
            {
                homePage2_ = new HomePage2();
                homepage2_Is = 1;
            }
            if (MainFrame.Content != homePage2_) MainFrame.Navigate(homePage2_);
            // SetbuttonColor
            ButtonForeground_Clean();
            home_button.Background= new SolidColorBrush(Color.FromRgb(64,53,130));
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (aifuckGame_Is == 0)
            {
                aifuckGame_ = new AifuckGame();
                aifuckGame_Is = 1;
            }
            if (MainFrame.Content != aifuckGame_) MainFrame.Navigate(aifuckGame_);
            ButtonForeground_Clean();
            autofuckgame_button.Background = new SolidColorBrush(Color.FromRgb(64,53,130));
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (crosshair_Is == 0)
            {
                crosshair_ = new Crosshair();
                crosshair_Is = 1;
            }
            if (MainFrame.Content != crosshair_) MainFrame.Navigate(crosshair_);
            ButtonForeground_Clean();
            crosshair_button.Background = new SolidColorBrush(Color.FromRgb(64,53,130));
        }
        private void Button_Click_About(object sender, RoutedEventArgs e)
        {
            ButtonForeground_Clean();
            about_button.Background = new SolidColorBrush(Color.FromRgb(64, 53, 130));
        }
        private void ButtonForeground_Clean()
        {
            home_button.Background = null;
            autofuckgame_button.Background = null;
            crosshair_button.Background= null;
            about_button.Background = null;
        }
        private string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApplyNecessaryConfiguration", "ScreenPosition(MainWindow).txt");
        private string temptext = "404 line0 MainWindow loaded";
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "ScreenPosition(MainWindow)_300_300_300_300");
            }
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
        // 这几个是公开的信息窗口的东西，Myprompt窗口可以访问这些信息来调整自己的位置和大小
        public static string? _title;
        public static string? _messageText;
        public static int? _messageTextFont;
        public static List<string>? _buttonTextList;
        public static int? _back;
        public (int Left, int Top, int Width, int Height) InfoMainWindow()
        {
            return ((int)Left, (int)Top, (int)Width, (int)Height);
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            _title = "退出";
            _messageText = "是否保存当前窗口位置？";
            _buttonTextList = new List<string> { "保存","不保存"};
            MyPrompt msgWin = new MyPrompt();
            msgWin.ShowDialog();
            if (_back == 0)
            {
                string[] temp_parts = File.ReadAllText(filePath)
                    .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                temptext = temp_parts[0];
                string[] saveParts = new string[]
                {
                temptext,
                Width.ToString(CultureInfo.InvariantCulture),
                Height.ToString(CultureInfo.InvariantCulture),
                Top.ToString(CultureInfo.InvariantCulture),
                Left.ToString(CultureInfo.InvariantCulture)
                };
                string output = string.Join("_", saveParts);
                File.WriteAllText(filePath, output);
            }
        }
    }
}