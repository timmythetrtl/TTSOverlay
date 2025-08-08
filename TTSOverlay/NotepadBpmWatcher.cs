using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;

namespace TTSOverlay
{
    public class NotepadBpmWatcher : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly AppViewModel _viewModel;
        private readonly Dispatcher _uiDispatcher;
        private readonly string _filePath;
        private readonly Timer _debounceTimer;
        private const int DebounceMs = 150; // avoid rapid duplicate triggers

        public NotepadBpmWatcher(string filePath, AppViewModel viewModel, Dispatcher uiDispatcher)
        {
            _filePath = filePath;
            _viewModel = viewModel;
            _uiDispatcher = uiDispatcher;
            _debounceTimer = new Timer(OnDebouncedFileChanged, null, Timeout.Infinite, Timeout.Infinite);

            _watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath) ?? ".")
            {
                Filter = Path.GetFileName(filePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (s, e) => _debounceTimer.Change(DebounceMs, Timeout.Infinite);
            _watcher.Renamed += (s, e) => _debounceTimer.Change(DebounceMs, Timeout.Infinite);

            // Initial read
            ReadAndApplyIfValid();
        }

        private void OnDebouncedFileChanged(object? state)
        {
            ReadAndApplyIfValid();
        }

        private void ReadAndApplyIfValid()
        {
            string? text = null;
            try
            {
                // Retry briefly in case file is locked by editor
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        text = File.ReadAllText(_filePath).Trim();
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(50);
                    }
                }

                if (string.IsNullOrEmpty(text))
                    return;

                // Only a single integer 
                if (Regex.IsMatch(text, @"^\d+$"))
                {
                    if (int.TryParse(text, out int bpmValue) && bpmValue > 0)
                    {
                        _uiDispatcher.Invoke(() =>
                        {
                            if (_viewModel.IsBpmMode)
                            {
                                // Avoid redundant sets
                                if (Math.Abs(_viewModel.InputBPM - bpmValue) > 0.001)
                                {
                                    _viewModel.InputBPM = bpmValue;
                                }
                            }
                        });
                    }
                }
                // else: ignore anything not a pure positive integer
            }
            catch
            {
                // swallow
            }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
        }
    }
}
