using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RimWorldModTranslate.Views
{
    public partial class ProgressWindow : Window
    {
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public ProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int current, int total, string currentItem)
        {
            var percentage = total > 0 ? (double)current / total * 100 : 0;
            ProgressBar!.Value = percentage;
            StatusText!.Text = $"Progress: {current}/{total} ({percentage:F1}%)";
            DetailsText!.Text = currentItem;
        }

        public void SetCompleted(int successCount, int errorCount)
        {
            ProgressBar!.Value = 100;
            StatusText!.Text = "Translation completed";
            DetailsText!.Text = $"Success: {successCount}, Errors: {errorCount}";
            CancelButton!.Content = "Close";
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            if (CancelButton?.Content?.ToString() == "Close")
            {
                Close();
            }
            else
            {
                CancellationTokenSource?.Cancel();
                StatusText!.Text = "Cancelling...";
                CancelButton!.IsEnabled = false;
            }
        }
    }
}
