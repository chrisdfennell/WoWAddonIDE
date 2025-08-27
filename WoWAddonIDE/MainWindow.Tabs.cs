// File: MainWindow.Tabs.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private System.Windows.Point? _tabDragStart;
        private TabItem? _draggedTab;

        // Close button handler (from XAML Tag binding)
        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            var tab = (sender as FrameworkElement)?.Tag as TabItem
                      ?? EditorTabs.SelectedItem as TabItem;
            if (tab != null) CloseTab(tab);
        }

        // Middle-click close on tab headers
        private void EditorTabs_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            var ti = FindAncestor<TabItem>(e.OriginalSource as DependencyObject);
            if (ti != null) CloseTab(ti);
        }

        // Call this once where you wire other global shortcuts
        private void EnsureCloseShortcutWired()
        {
            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ =>
                {
                    if (EditorTabs.SelectedItem is TabItem sel) CloseTab(sel);
                }),
                new KeyGesture(Key.W, ModifierKeys.Control)));
        }

        private void CloseTab(TabItem tab)
        {
            // Prompt if dirty (header ends with '*')
            var header = tab.Header as string ?? tab.Header?.ToString() ?? "";
            bool isDirty = header.EndsWith('*'); // char overload avoids CA1866

            if (isDirty)
            {
                var name = header.TrimEnd('*');
                var res = MessageBox.Show(this,
                    $"Save changes to {name}?",
                    "Close Tab",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Cancel) return;
                if (res == MessageBoxResult.Yes)
                {
                    try { SaveTab(tab); } catch { /* ignore */ }
                }
            }

            EditorTabs.Items.Remove(tab);
        }

        // ───── Drag & Drop reorder ─────

        private void EditorTabs_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _tabDragStart = e.GetPosition(null);
            _draggedTab = FindAncestor<TabItem>(e.OriginalSource as DependencyObject);
        }

        private void EditorTabs_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedTab == null || !_tabDragStart.HasValue)
                return;

            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _tabDragStart.Value.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(pos.Y - _tabDragStart.Value.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                DragDrop.DoDragDrop(
                    _draggedTab,
                    new System.Windows.DataObject(typeof(TabItem), _draggedTab),
                    System.Windows.DragDropEffects.Move);
            }
        }

        private void EditorTabs_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(TabItem))
                ? System.Windows.DragDropEffects.Move
                : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private void EditorTabs_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TabItem))) return;

            var sourceTab = (TabItem)e.Data.GetData(typeof(TabItem))!;
            var targetTab = FindAncestor<TabItem>(e.OriginalSource as DependencyObject);

            int srcIndex = EditorTabs.Items.IndexOf(sourceTab);
            if (srcIndex < 0) return;

            int dstIndex = targetTab != null ? EditorTabs.Items.IndexOf(targetTab)
                                             : EditorTabs.Items.Count - 1;

            if (dstIndex < 0) dstIndex = EditorTabs.Items.Count - 1;
            if (dstIndex == srcIndex) return;

            EditorTabs.Items.Remove(sourceTab);
            EditorTabs.Items.Insert(dstIndex, sourceTab);
            EditorTabs.SelectedItem = sourceTab;

            _draggedTab = null;
            _tabDragStart = null;
        }

        // Utility to walk up visual tree
        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}