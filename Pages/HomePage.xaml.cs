using ILGPU.Runtime;
using ILGPU;
using System;
using System.Collections.Generic;
using System.IO;
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
using WindowsAPICodePack.Dialogs;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using Path = System.IO.Path;
using ILGPU.Runtime.OpenCL;
using System.Reflection;
using CSScripting;
using CSScriptLib;

namespace NavigationViewExample.Pages
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string scriptPath = @"E:\homepage.cs"; // 文件路径
            string code = File.ReadAllText(scriptPath); // 读取
            Assembly asm = CSScriptLib.CSScript.Evaluator.CompileCode(code); // 运行
            dynamic script = asm.CreateObject("*");
            this.DataContext = script; // 绑定到 XAML
            int result = script.Calculate(); // 调用方法
            MessageBox.Show($"计算结果: {result}");
        }
    }
}
