using ArcadeStick.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ArcadeStick.Views
{
    public partial class BatchConvertWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private List<string> _pending = new();
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        // DataContext is the live ConfigurationSettings (same as SortingWindow) so every theme binding in the
        // XAML resolves. The count pill and progress text are set from code-behind.
        public BatchConvertWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel.Configuration;

            LoadPendingList();

            // Closing mid-run cancels the batch; the video being converted at that moment is left untouched.
            Closing += (s, e) => _cts?.Cancel();
        }

        // Re-reads which videos the cache currently marks unplayable and resets the count pill/status text.
        private void LoadPendingList()
        {
            _pending = _viewModel.GetUnplayableVideoPaths();
            TxtToConvertCount.Text = _pending.Count.ToString();
            BtnStart.IsEnabled = _pending.Count > 0;
            TxtCurrentFile.Text = string.Empty;
            TxtResult.Text = _pending.Count == 0
                ? "No videos need converting."
                : $"Ready to convert {_pending.Count} video(s).";
        }

        // One button, two roles: starts the batch when idle, cancels it while running.
        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                _cts?.Cancel();
                BtnStart.IsEnabled = false;
                TxtCurrentFile.Text = "Cancelling...";
                return;
            }

            await RunBatchAsync();
        }

        private async Task RunBatchAsync()
        {
            var queue = new List<string>(_pending);
            int total = queue.Count;
            if (total == 0) return;

            int converted = 0;
            var failed = new List<string>();
            bool cancelled = false;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            BtnStart.Content = "Cancel";
            ConversionProgress.Maximum = total;
            ConversionProgress.Value = 0;
            TxtResult.Text = string.Empty;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    token.ThrowIfCancellationRequested();

                    string path = queue[i];
                    TxtCurrentFile.Text = $"Converting {i + 1} of {total}: {Path.GetFileName(path)}";

                    if (await _viewModel.ConvertVideoInPlaceAsync(path, token)) converted++;
                    else failed.Add(Path.GetFileName(path));

                    ConversionProgress.Value = i + 1;
                    TxtToConvertCount.Text = (total - i - 1).ToString();
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _isRunning = false;

                // A converted video may now play for the selected game.
                _viewModel.RefreshAfterBatchConversion();
            }

            BtnStart.Content = "Start Conversion";
            LoadPendingList();

            string summary = cancelled
                ? $"Cancelled. Converted {converted} of {total}."
                : $"Finished. Converted {converted} of {total}.";
            if (failed.Count > 0)
            {
                summary += $"\n{failed.Count} failed and were left as they were: {string.Join(", ", failed)}";
            }
            TxtResult.Text = summary;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}