// Windows/AboutWindow.xaml.cs
using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace WoWAddonIDE.Windows
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionRun.Text = (Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0)).ToString();

            LicenseText.Text =
@"Copyright (c) 2025 Chris Fennell

Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
and associated documentation files (the ""Software""), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial 
portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO 
EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER 
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
USE OR OTHER DEALINGS IN THE SOFTWARE.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void OpenRepo_Click(object sender, RoutedEventArgs e)
            => Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/chrisdfennell/WoWAddonIDE",
                UseShellExecute = true
            });

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            e.Handled = true;
        }
    }
}