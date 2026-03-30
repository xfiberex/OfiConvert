using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;
using OfiConvert.ViewModels;

namespace OfiConvert;

public partial class MainWindow : FluentWindow
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.CanClose())
        {
            var result = System.Windows.MessageBox.Show(
                "Hay una conversión en curso. Si cierras ahora, los procesos de Office podrían quedar abiertos.\n\n¿Deseas cancelar la conversión y salir?",
                "Confirmar cierre",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            ViewModel.CancelConversionCommand.Execute(null);
        }
    }
}

