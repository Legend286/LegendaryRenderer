using System;
using System.Text;
using System.Threading;

namespace LegendaryRenderer.LegendaryRuntime.Application.ProgressReporting;

public class ConsoleProgressBar : IDisposable
{
    private const int BlockCount = 20;                                            // Number of blocks in the progress bar
    private const char ProgressCharacter = '■';                                   // Character for completed part
    private const char BackgroundCharacter = '-';                                 // Character for remaining part
    private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0 / 8); // Animation speed
    private readonly Timer _timer;

    private double _currentProgress = 0;
    private string _currentText = string.Empty;
    private bool _disposed = false;
    private int _animationIndex = 0;

    // Characters for the spinning animation
    private static readonly char[] AnimationChars = new[] { '|', '/', '-', '\\' };

    public ConsoleProgressBar()
    {
        _timer = new Timer(TimerHandler);

        // Hide the cursor to prevent flickering
        Console.CursorVisible = false;
    }

    /// <summary>
    /// Updates the progress of the bar.
    /// </summary>
    /// <param name="value">The current progress value (0.0 to 1.0).</param>
    /// <param name="text">Optional text to display next to the progress bar.</param>
    public void Report(double value, string text = "")
    {
        // Make sure value is in [0..1] range
        value = Math.Max(0, Math.Min(1, value));
        Interlocked.Exchange(ref _currentProgress, value);
        Interlocked.Exchange(ref _currentText, text);

        // Start the timer if it hasn't been started
        if (!_timer.Change(TimeSpan.Zero, _animationInterval))
        {
            // This is a safety net, should not normally be hit if Dispose is called correctly.
        }
    }

    private void TimerHandler(object state)
    {
        lock (_timer)
        {
            if (_disposed) return;

            int progressBlockCount = (int)(_currentProgress * BlockCount);
            int percent = (int)(_currentProgress * 100);

            // Build the progress bar string
            StringBuilder sb = new StringBuilder("[");
            sb.Append(ProgressCharacter, progressBlockCount);
            sb.Append(BackgroundCharacter, BlockCount - progressBlockCount);
            sb.Append("] ");

            // Add percentage
            sb.AppendFormat("{0,3}% ", percent);

            // Add spinning animation character
            sb.Append(AnimationChars[_animationIndex++ % AnimationChars.Length]);

            // Add custom text if any
            if (!string.IsNullOrEmpty(_currentText))
            {
                sb.Append(" ");
                sb.Append(_currentText);
            }

            // Ensure the output doesn't exceed console width by clearing the previous line
            // and then writing the new one.
            // Get current cursor position
            int originalTop = Console.CursorTop;
            int originalLeft = Console.CursorLeft;

            // Clear the current line (important for varying text lengths)
            Console.SetCursorPosition(0, originalTop);
            Console.Write(new string(' ', Console.WindowWidth - 1)); // Clear the line
            Console.SetCursorPosition(0, originalTop);               // Reset cursor to the beginning of the line

            // Write the progress bar
            Console.Write(sb.ToString());


            // If progress is 100%, stop the timer and move to a new line
            if (_currentProgress >= 1)
            {
                StopTimer();
                Console.WriteLine(); // Move to the next line after completion
            }
        }
    }

    private void StopTimer()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        lock (_timer) // Ensure TimerHandler is not running
        {
            _currentProgress = 1; // Ensure it shows 100%
            // Final update without animation character
            int progressBlockCount = (int)(_currentProgress * BlockCount);
            int percent = (int)(_currentProgress * 100);
            string text = $"[{new string(ProgressCharacter, progressBlockCount)}{new string(BackgroundCharacter, BlockCount - progressBlockCount)}] {percent,3}% {_currentText} Done!";

            int originalTop = Console.CursorTop;
            Console.SetCursorPosition(0, originalTop);
            Console.Write(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(0, originalTop);
            Console.Write(text);
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Disposes of the progress bar, ensuring the timer is stopped and cursor is made visible.
    /// </summary>
    public void Dispose()
    {
        lock (_timer)
        {
            _disposed = true;
            StopTimer();
            _timer.Dispose();
        }
        // Restore the cursor
        Console.CursorVisible = true;
    }
}