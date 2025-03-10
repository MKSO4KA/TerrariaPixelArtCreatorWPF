﻿
using System.Windows;
using System.Windows.Media;

namespace WpfApp1
{
    public static class MessageService
    {
        public static void ShowError(string message, string title = "Error")
        {
            var dialog = new CustomMessageBox();
            var vm = new MessageViewModel
            {
                Title = title,
                Message = message,
                Icon = Geometry.Parse("M13,13H11V7H13M13,17H11V15H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z"),
                IconColor = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFB00020"))
            };

            dialog.DataContext = vm;
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        }
        public static void ShowSuccess(string message, string title = "Success")
        {
            var dialog = new CustomMessageBox();
            var vm = new MessageViewModel
            {
                Title = title,
                Message = message,
                Icon = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"),
                IconColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4CAF50"))
            };

            dialog.DataContext = vm;
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        }
    }
}
