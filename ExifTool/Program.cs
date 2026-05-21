using Avalonia;
using System;

namespace ExifTool
{
    internal sealed class Program
    {
        /// <summary>
        /// Starts the desktop application.
        /// </summary>
        /// <param name="args">Command-line arguments supplied by the operating system.</param>
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        /// <summary>
        /// Builds the cross-platform Avalonia app configuration.
        /// </summary>
        /// <returns>The configured Avalonia app builder.</returns>
        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools(_ => { })
#endif
                .WithInterFont()
                .LogToTrace();
    }
}
