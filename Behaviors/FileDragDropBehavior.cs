using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;
using OfiConvert.ViewModels;

namespace OfiConvert.Behaviors;

public class FileDragDropBehavior : Behavior<FrameworkElement>
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(MainViewModel),
            typeof(FileDragDropBehavior),
            new PropertyMetadata(null));

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty HighlightBorderProperty =
        DependencyProperty.Register(
            nameof(HighlightBorder),
            typeof(Border),
            typeof(FileDragDropBehavior),
            new PropertyMetadata(null));

    public Border? HighlightBorder
    {
        get => (Border?)GetValue(HighlightBorderProperty);
        set => SetValue(HighlightBorderProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.AllowDrop = true;
        AssociatedObject.DragEnter += OnDragEnter;
        AssociatedObject.DragLeave += OnDragLeave;
        AssociatedObject.Drop += OnDrop;
        AssociatedObject.DragOver += OnDragOver;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.DragEnter -= OnDragEnter;
        AssociatedObject.DragLeave -= OnDragLeave;
        AssociatedObject.Drop -= OnDrop;
        AssociatedObject.DragOver -= OnDragOver;

        base.OnDetaching();
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            SetHighlightState(true);
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        SetHighlightState(false);
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) 
            ? DragDropEffects.Copy 
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        SetHighlightState(false);

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files is not null && files.Length > 0 && ViewModel is not null)
            {
                ViewModel.AddFiles(files);
            }
        }

        e.Handled = true;
    }

    private void SetHighlightState(bool isHighlighted)
    {
        if (HighlightBorder is null) return;

        if (isHighlighted)
        {
            HighlightBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 120, 212));
            HighlightBorder.BorderThickness = new Thickness(3);
        }
        else
        {
            HighlightBorder.BorderBrush = Application.Current.TryFindResource("ControlStrokeColorDefaultBrush") 
                as System.Windows.Media.Brush 
                ?? new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(224, 224, 224));
            HighlightBorder.BorderThickness = new Thickness(2);
        }
    }
}
