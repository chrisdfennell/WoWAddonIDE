// Windows/ColorPickerWindow.xaml.cs
using System;
using System.Globalization;
using System.Windows;
using Media = System.Windows.Media;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class ColorPickerWindow : Window
    {
        public Media.Color SelectedColor { get; private set; }

        public ColorPickerWindow(Media.Color start)
        {
            InitializeComponent();
            SelectedColor = start;
            SetUIFromColor(start);
        }

        private void SetUIFromColor(Media.Color c)
        {
            A.Value = c.A; R.Value = c.R; G.Value = c.G; B.Value = c.B;
            AT.Text = c.A.ToString(); RT.Text = c.R.ToString(); GT.Text = c.G.ToString(); BT.Text = c.B.ToString();
            Preview.Background = new Media.SolidColorBrush(c);
            ArgbText.Text = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            WowText.Text = WowColor.ToWowCode(c);
            HexBox.Text = $"{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private void AnyChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var c = Media.Color.FromArgb((byte)A.Value, (byte)R.Value, (byte)G.Value, (byte)B.Value);
            SelectedColor = c;
            SetUIFromColor(c);
        }

        private void AT_TextChanged(object s, System.Windows.Controls.TextChangedEventArgs e) => TryBox(AT, v => A.Value = v);
        private void RT_TextChanged(object s, System.Windows.Controls.TextChangedEventArgs e) => TryBox(RT, v => R.Value = v);
        private void GT_TextChanged(object s, System.Windows.Controls.TextChangedEventArgs e) => TryBox(GT, v => G.Value = v);
        private void BT_TextChanged(object s, System.Windows.Controls.TextChangedEventArgs e) => TryBox(BT, v => B.Value = v);

        private void TryBox(System.Windows.Controls.TextBox tb, Action<byte> set)
        {
            if (byte.TryParse(tb.Text, out var b)) set(b);
        }

        private void HexBox_TextChanged(object s, System.Windows.Controls.TextChangedEventArgs e)
        {
            var t = HexBox.Text?.Trim();
            if (string.IsNullOrEmpty(t) || t.Length != 8) return;
            if (uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            {
                var c = Media.Color.FromArgb(
                    (byte)((argb >> 24) & 0xFF),
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));
                SelectedColor = c;
                SetUIFromColor(c);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}