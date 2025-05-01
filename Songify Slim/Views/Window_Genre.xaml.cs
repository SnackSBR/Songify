using MahApps.Metro.Controls.Dialogs;
using Songify_Slim.Util.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Songify_Slim.Views
{
    /// <summary>
    ///     This window dispalys and manages the blacklist
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public partial class Window_Genre
    {
        public Window_Genre()
        {
            InitializeComponent();
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadBlacklist();
        }

        public void LoadBlacklist()
        {
            LoadGenreBlacklist();
        }

        private void LoadGenreBlacklist()
        {
            ListView_Blacklist.Items.Clear();

            if (Settings.GenreBlacklist == null || Settings.GenreBlacklist.Count == 0)
                return;

            foreach (string s in Settings.GenreBlacklist.Where(s => !string.IsNullOrEmpty(s)))
                ListView_Blacklist.Items.Add(s);
        }

        private void btn_Add_Click(object sender, RoutedEventArgs e)
        {
            //This adds to the blacklist.
            AddToBlacklist(tb_Blacklist.Text);
            tb_Blacklist.Text = "";
        }

        private async void AddToBlacklist(string search)
        {
            //Check if the string is empty
            if (string.IsNullOrEmpty(search))
                return;

            ListView_Blacklist.Items.Add(search);
            SaveBlacklist();
        }

        public void SaveBlacklist()
        {
            List<string> tempList = new();
            //Genre Blacklist
            if (ListView_Blacklist.Items.Count > 0)
            {
                tempList.AddRange(from object item in ListView_Blacklist.Items where (string)item != "" select (string)item);

            }
            Settings.GenreBlacklist = tempList;
            ConfigHandler.WriteAllConfig(Settings.Export());
            Settings.Export();
            LoadBlacklist();
        }

        private async void btn_Clear_Click(object sender, RoutedEventArgs e)
        {
            MessageDialogResult msgResult = await this.ShowMessageAsync("Notification",
                        "Do you really want to clear the Genre blocklist?", MessageDialogStyle.AffirmativeAndNegative,
                        new MetroDialogSettings { AffirmativeButtonText = "Yes", NegativeButtonText = "No" });
            if (msgResult == MessageDialogResult.Affirmative)
            {
                Settings.GenreBlacklist.Clear();
                ListView_Blacklist.Items.Clear();
            }
        }

        private async void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem mnu)) return;

            ListBox listView = ((ContextMenu)mnu.Parent).PlacementTarget as ListBox;

            // right-click context menu to delete single blacklist entries
            if (listView != null && listView.SelectedItem == null)
                return;

            if (listView == null) return;
            MessageDialogResult msgResult = await this.ShowMessageAsync("Notification",
                "Delete " + listView.SelectedValue + "?", MessageDialogStyle.AffirmativeAndNegative,
                new MetroDialogSettings { AffirmativeButtonText = "Yes", NegativeButtonText = "No" });
            if (msgResult != MessageDialogResult.Affirmative) return;
            listView.Items.Remove(listView.SelectedItem);
            SaveBlacklist();
        }

        private void tb_Blacklist_KeyDown(object sender, KeyEventArgs e)
        {
            // on enter key save to the blacklist
            if (e.Key == Key.Enter)
            {
                AddToBlacklist(tb_Blacklist.Text);
                tb_Blacklist.Text = "";
            }
        }

        private async void ListView_Blacklist_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                MessageDialogResult msgResult = await this.ShowMessageAsync("Notification",
                    "Delete " + ListView_Blacklist.SelectedItem + "?", MessageDialogStyle.AffirmativeAndNegative,
                    new MetroDialogSettings { AffirmativeButtonText = "Yes", NegativeButtonText = "No" });
                if (msgResult == MessageDialogResult.Affirmative)
                {
                    ListView_Blacklist.Items.Remove(ListView_Blacklist.SelectedItem);
                    SaveBlacklist();
                }
            }
        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveBlacklist();
        }
    }
}
