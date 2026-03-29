// File: UIErrorNotifier.cs
using System;

namespace MinimalFirewall
{
    /// <summary>
    /// Thread-safe static event bus for surfacing background service errors to the UI.
    /// The main window subscribes to <see cref="ErrorOccurred"/> and shows an InfoBar or dialog.
    /// </summary>
    public static class UIErrorNotifier
    {
        /// <summary>Raised when a background service encounters a user-visible error.</summary>
        public static event Action<string, string>? ErrorOccurred;

        /// <summary>Raises the error event (safe to call from any thread).</summary>
        public static void Notify(string message, string title = "Error")
        {
            System.Diagnostics.Debug.WriteLine($"[UIError] {title}: {message}");
            ErrorOccurred?.Invoke(message, title);
        }
    }
}
