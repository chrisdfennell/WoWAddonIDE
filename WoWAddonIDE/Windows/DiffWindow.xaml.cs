using System.Windows;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace WoWAddonIDE.Windows
{
    public partial class DiffWindow : Window
    {
        public DiffWindow()
        {
            InitializeComponent();
        }

        public void ShowDiff(string leftText, string rightText)
        {
            var d = InlineDiffBuilder.Diff(leftText, rightText);
            Left.Text = BuildSide(d, left: true);
            Right.Text = BuildSide(d, left: false);
        }

        private static string BuildSide(DiffPaneModel d, bool left)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in d.Lines)
            {
                char tag = ' ';
                string text = line.Text ?? "";
                if (line.Type == ChangeType.Inserted) tag = left ? ' ' : '+';
                else if (line.Type == ChangeType.Deleted) tag = left ? '-' : ' ';
                else if (line.Type == ChangeType.Modified) tag = left ? '~' : '~';
                sb.Append(tag).Append(' ').AppendLine(text);
            }
            return sb.ToString();
        }
    }
}
