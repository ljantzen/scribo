using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Scribo.Views;

public partial class ConfirmDeleteWindow : Window
{
    public bool Result { get; private set; } = false;

    public ConfirmDeleteWindow()
    {
        // Set theme and background before InitializeComponent
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Background = Brushes.White;
        
        // Add a class for specific styling
        Classes.Add("ConfirmDeleteDialog");
        
        InitializeComponent();
        
        // Ensure all backgrounds are set after initialization
        Background = Brushes.White;
        
        if (Content is Panel panel)
        {
            panel.Background = Brushes.White;
        }
        
        if (MessageTextBlock != null)
        {
            MessageTextBlock.Foreground = Brushes.Black;
        }
    }

    public ConfirmDeleteWindow(string message) : this()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        
        if (MessageTextBlock != null)
        {
            MessageTextBlock.Text = message;
            MessageTextBlock.Foreground = Brushes.Black;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        
        // Force light theme
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        
        // Ensure all backgrounds are white
        Background = Brushes.White;
        
        if (Content is Panel panel)
        {
            panel.Background = Brushes.White;
        }
        
        if (MessageTextBlock != null)
        {
            MessageTextBlock.Foreground = Brushes.Black;
        }
        
        // Override all visual descendants to ensure white backgrounds
        void ApplyWhiteBackgrounds()
        {
            Background = Brushes.White;
            
            if (Content is Panel contentPanel)
            {
                contentPanel.Background = Brushes.White;
            }
            
            if (MessageTextBlock != null)
            {
                MessageTextBlock.Foreground = Brushes.Black;
            }
            
            // Force backgrounds on all child elements
            var scrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
            if (scrollViewer != null)
            {
                scrollViewer.Background = Brushes.White;
            }
            
            var buttonBorder = this.FindControl<Border>("ButtonBorder");
            if (buttonBorder != null)
            {
                buttonBorder.Background = Brushes.White;
            }
            
            var mainGrid = this.FindControl<Grid>("MainGrid");
            if (mainGrid != null)
            {
                mainGrid.Background = Brushes.White;
            }
            
            // Also override any Panel, Border, or ScrollViewer in the visual tree
            foreach (var panel in this.GetVisualDescendants().OfType<Panel>())
            {
                if (panel.Background == null || panel.Background is SolidColorBrush brush && brush.Color != Colors.White)
                {
                    panel.Background = Brushes.White;
                }
            }
            
            foreach (var border in this.GetVisualDescendants().OfType<Border>())
            {
                if (border.Background == null || border.Background is SolidColorBrush brush && brush.Color != Colors.White)
                {
                    border.Background = Brushes.White;
                }
            }
            
            foreach (var sv in this.GetVisualDescendants().OfType<ScrollViewer>())
            {
                if (sv.Background == null || sv.Background is SolidColorBrush brush && brush.Color != Colors.White)
                {
                    sv.Background = Brushes.White;
                }
            }
            
            foreach (var textBlock in this.GetVisualDescendants().OfType<TextBlock>())
            {
                if (textBlock.Foreground == null || textBlock.Foreground is SolidColorBrush brush && brush.Color != Colors.Black)
                {
                    textBlock.Foreground = Brushes.Black;
                }
            }
            
            InvalidateVisual();
        }
        
        // Apply immediately
        ApplyWhiteBackgrounds();
        
        // Apply again after layout completes
        Avalonia.Threading.Dispatcher.UIThread.Post(ApplyWhiteBackgrounds, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        e.Handled = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        e.Handled = true;
        Close(false);
    }
}
