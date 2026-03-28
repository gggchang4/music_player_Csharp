using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using MusicPlayerApp.ViewModels;
using MusicPlayerApp.Services;

namespace MusicPlayerApp.Views
{
    public partial class AlbumsUserControl : UserControl
    {
        public AlbumsUserControl()
        {
            InitializeComponent();
        }
        
        private void FavoriteAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null && DataContext is AlbumsViewModel viewModel)
            {
                int albumId = Convert.ToInt32(button.Tag);
                var album = viewModel.Albums.FirstOrDefault(a => a.Id == albumId);
                if (album != null)
                {
                    viewModel.ToggleFavoriteAlbumCommand.Execute(album);
                    e.Handled = true;
                }
            }
        }
    }
} 