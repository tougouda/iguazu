using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Iguazu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// The file extension allowed for drag and drop.
        /// </summary>
        const string DRAG_AND_DROP_FILE_EXTENSION = ".mp3";

        /// <summary>
        /// The view-model.
        /// </summary>
        private MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel(this);
        }

        /// <summary>
        /// Handles the close command.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event arguments.</param>
        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        /// <summary>
        /// Handles the properties command.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event arguments.</param>
        private void PropertiesCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            OpenPropertiesDialog();
        }

        public void OpenPropertiesDialog()
        {
            var propertiesWindows = new PropertiesWindow();
            propertiesWindows.Owner = this;
            propertiesWindows.ShowDialog();
        }

        /// <summary>
        /// The action that is done when you click on the choose button.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event arguments.</param>
        private void ChooseFile_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Filter = "Fichiers audio MP3 (*.mp3)|*.mp3";

            if (openFileDialog.ShowDialog() == true)
            {
                ViewModel.AudioFilePath = openFileDialog.FileName;
                ViewModel.ResetStatus();
            }
        }

        private async void Transcribe_Button_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.Transcribe();
        }

        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.Filter = "Fichiers texte (*.txt)|*.txt";

            if (saveFileDialog.ShowDialog() == true)
            {
                ViewModel.Save(saveFileDialog.FileName);
            }
        }

        private void AudioFile_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process p = new Process();
            p.StartInfo.FileName = "explorer";
            p.StartInfo.Arguments = "\"" + ViewModel.AudioFilePath + "\"";
            p.Start();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.Length > 0 && Path.GetExtension(files[0]).ToLower() == DRAG_AND_DROP_FILE_EXTENSION)
                {
                    // Assuming you have one file that you care about, pass it off to whatever
                    // handling code you have defined.
                    ViewModel.AudioFilePath = files[0];
                    ViewModel.ResetStatus();
                }
            }
        }

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.GoogleCredentialsFilePath == "")
            {
                OpenPropertiesDialog();
            }
        }
    }
}
