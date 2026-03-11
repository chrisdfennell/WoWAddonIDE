// Windows/BuildTargetsWindow.xaml.cs
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace WoWAddonIDE.Windows
{
    public partial class BuildTargetsWindow : Window
    {
        public class TargetRow
        {
            public bool IsChecked { get; set; }
            public string DisplayName { get; set; } = "";
            public string Interface { get; set; } = ""; // e.g. 110005
            public string Suffix { get; set; } = "";    // e.g. Retail
            public string FlavorId { get; set; } = "";  // internal tag
        }

        public List<TargetRow> Targets { get; } = new();

        public BuildTargetsWindow(string? defaultInterfaceFromProject)
        {
            InitializeComponent();

            string def = string.IsNullOrWhiteSpace(defaultInterfaceFromProject) ? "" : defaultInterfaceFromProject;

            Targets.Add(new TargetRow { IsChecked = true, DisplayName = "Retail (_retail_)", FlavorId = "retail", Interface = def, Suffix = "Retail" });
            Targets.Add(new TargetRow { IsChecked = false, DisplayName = "Classic Era (_classic_)", FlavorId = "classic_era", Interface = def, Suffix = "Classic" });
            Targets.Add(new TargetRow { IsChecked = false, DisplayName = "Hardcore (_classic_era_)", FlavorId = "hardcore", Interface = def, Suffix = "Hardcore" });
            Targets.Add(new TargetRow { IsChecked = false, DisplayName = "Wrath (_classic_) WotLK", FlavorId = "wrath", Interface = def, Suffix = "Wrath" });
            Targets.Add(new TargetRow { IsChecked = false, DisplayName = "PTR (_ptr_)", FlavorId = "ptr", Interface = def, Suffix = "PTR" });

            Grid.ItemsSource = Targets;
        }

        public List<TargetRow> SelectedTargets() =>
            Targets.Where(t => t.IsChecked && !string.IsNullOrWhiteSpace(t.Interface)).ToList();

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTargets().Count == 0)
            {
                MessageBox.Show(this, "Pick at least one target and set its Interface number.", "Multi-Zip", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}