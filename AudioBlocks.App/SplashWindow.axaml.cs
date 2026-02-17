using Avalonia.Controls;
using Avalonia.Threading;
using System;

namespace AudioBlocks.App
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public void SetStatus(string text)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (StatusText != null)
                    StatusText.Text = text;
            });
        }
    }
}
