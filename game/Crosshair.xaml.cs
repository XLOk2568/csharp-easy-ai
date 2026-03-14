using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XLOKProject
{
    /// <summary>
    /// Crosshair.xaml 的交互逻辑
    /// </summary>
    public partial class Crosshair : Page
    {
        public Crosshair()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            myEllipse.Fill= new SolidColorBrush(Colors.Red);
            myEllipse.Fill= Brushes.Transparent;
        }
    }
}
