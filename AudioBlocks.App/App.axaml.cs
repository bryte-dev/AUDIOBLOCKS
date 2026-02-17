using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace AudioBlocks.App
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var splash = new SplashWindow();
                desktop.MainWindow = splash;
                splash.Show();

                splash.SetStatus("Initializing audio engine...");
                await Task.Delay(400);

                splash.SetStatus("Loading effects...");
                await Task.Delay(300);

                splash.SetStatus("Preparing UI...");
                var main = new MainWindow();
                await Task.Delay(200);

                splash.SetStatus("Ready");
                await Task.Delay(100);

                desktop.MainWindow = main;
                main.Show();
                splash.Close();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}