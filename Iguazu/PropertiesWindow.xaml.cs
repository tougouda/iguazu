using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;

namespace Iguazu
{
    /// <summary>
    /// Interaction logic for PropertiesWindow.xaml
    /// </summary>
    public partial class PropertiesWindow : Window
    {
        public PropertiesWindow()
        {
            InitializeComponent();
            path.Text = Properties.Settings.Default.GoogleCredentialsFilePath;
            bucket.Text = Properties.Settings.Default.GoogleStorageBucketName;
            speakersCount.SelectedValue = Properties.Settings.Default.SpeakersDefaultCount;
        }

        /// <summary>
        /// Handles the close command.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event arguments.</param>
        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Handles the save command.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event arguments.</param>
        private void SaveCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            if (Properties.Settings.Default.GoogleCredentialsFilePath != path.Text)
            {
                MessageBox.Show(this, "Vous devez redémarrer l’application pour que les changements soient pris en compte.", "Attention", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Properties.Settings.Default.GoogleCredentialsFilePath = path.Text;
            Properties.Settings.Default.GoogleStorageBucketName = bucket.Text;
            Properties.Settings.Default.SpeakersDefaultCount = int.Parse(speakersCount.SelectedValue.ToString());
            Properties.Settings.Default.Save();

            Close();
        }

        /// <summary>
        /// The action that is done when you click on the choose button.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event arguments.</param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Filter = "Fichiers JSON (*.json)|*.json";

            if (openFileDialog.ShowDialog() == true)
            {
                path.Text = openFileDialog.FileName;
            }
        }
    }
}
