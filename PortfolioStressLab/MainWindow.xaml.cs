using PortfolioStressLab.Wpf.ViewModels;
using System.Windows;

namespace PortfolioStressLab.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = (MainViewModel)App.Services.GetService(typeof(MainViewModel))!;
        }
    }
}
