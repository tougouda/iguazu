using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Iguazu
{
    class MainViewModel : INotifyPropertyChanged
    {
        public enum Status
        {
            NotStarted = 0,
            Pending = 1,
            Done = 2,
            Error = 3
        }

        /// <summary>
        /// The application version string.
        /// </summary>
        public string AppVersion { get; } = "Version " + Assembly.GetEntryAssembly().GetName().Version.ToString();

        /// <summary>
        /// The owner.
        /// </summary>
        public MainWindow Owner { get; set; }

        /// <summary>
        /// The audio file path.
        /// </summary>
        private string _audioFilePath = "";
        /// <summary>
        /// The audio file path.
        /// </summary>
        public string AudioFilePath
        {
            get => _audioFilePath;
            set
            {
                if (_audioFilePath != value)
                {
                    _audioFilePath = value;
                    RaisePropertyChanged("AudioFilePath");
                    RaisePropertyChanged("CanTranscribe");
                }
            }
        }
        /// <summary>
        /// The audio file speakers count.
        /// </summary>
        public int SpeakersCount { get; set; } = Properties.Settings.Default.SpeakersDefaultCount;

        /// <summary>
        /// Whether the form can be modified.
        /// </summary>
        public bool CanChangeForm { get => !IsWorking; }

        /// <summary>
        /// Whether the transcribe action can be launched.
        /// </summary>
        public bool CanTranscribe { get => !AudioFilePath.Equals(string.Empty) && !IsWorking; }

        /// <summary>
        /// The audio file transcript.
        /// </summary>
        private string _transcript = "";
        /// <summary>
        /// The audio file transcript.
        /// </summary>
        public string Transcript
        {
            get => _transcript;
            set
            {
                if (_transcript != value)
                {
                    _transcript = value;
                    RaisePropertyChanged("Transcript");
                }
            }
        }

        /// <summary>
        /// Whether the transcribing process is pending.
        /// </summary>
        private bool _isWorking = false;
        /// <summary>
        /// Whether the transcribing process is pending.
        /// </summary>
        public bool IsWorking
        {
            get => _isWorking;
            set
            {
                if (_isWorking != value)
                {
                    _isWorking = value;
                    RaisePropertyChanged("IsWorking");
                    RaisePropertyChanged("CanChangeForm");
                    RaisePropertyChanged("CanTranscribe");
                }
            }
        }

        /// <summary>
        /// The audio file reencoding progression.
        /// </summary>
        private string _ReencodingProgress;
        /// <summary>
        /// The audio file reencoding progression.
        /// </summary>
        public string ReencodingProgress
        {
            get => _ReencodingProgress;
            set
            {
                if (_ReencodingProgress != value)
                {
                    _ReencodingProgress = value;
                    RaisePropertyChanged("ReencodingProgress");
                }
            }
        }

        /// <summary>
        /// The audio file reencoding status.
        /// </summary>
        private Status _ReencodingStatus = Status.NotStarted;
        /// <summary>
        /// The audio file reencoding status.
        /// </summary>
        public Status ReencodingStatus
        {
            get => _ReencodingStatus;
            set
            {
                if (_ReencodingStatus != value)
                {
                    _ReencodingStatus = value;
                    RaisePropertyChanged("ReencodingStatus");
                }
            }
        }

        /// <summary>
        /// The Google Storage uploading progression.
        /// </summary>
        private string _GSUploadProgress;
        /// <summary>
        /// The Google Storage uploading progression.
        /// </summary>
        public string GSUploadProgress
        {
            get => _GSUploadProgress;
            set
            {
                if (_GSUploadProgress != value)
                {
                    _GSUploadProgress = value;
                    RaisePropertyChanged("GSUploadProgress");
                }
            }
        }

        /// <summary>
        /// The Google Storage uploading status.
        /// </summary>
        private Status _GSUploadStatus = Status.NotStarted;
        /// <summary>
        /// The Google Storage uploading status.
        /// </summary>
        public Status GSUploadStatus
        {
            get => _GSUploadStatus;
            set
            {
                if (_GSUploadStatus != value)
                {
                    _GSUploadStatus = value;
                    RaisePropertyChanged("GSUploadStatus");
                }
            }
        }

        /// <summary>
        /// The transcribing status.
        /// </summary>
        private Status _TranscribingStatus = Status.NotStarted;
        /// <summary>
        /// The transcribing status.
        /// </summary>
        public Status TranscribingStatus
        {
            get => _TranscribingStatus;
            set
            {
                if (_TranscribingStatus != value)
                {
                    _TranscribingStatus = value;
                    RaisePropertyChanged("TranscribingStatus");
                    RaisePropertyChanged("CanSaveTranscript");
                }
            }
        }

        /// <summary>
        /// The Google Storage deletion status.
        /// </summary>
        private Status _GSDeleteStatus = Status.NotStarted;
        /// <summary>
        /// The Google Storage deletion status.
        /// </summary>
        public Status GSDeleteStatus
        {
            get => _GSDeleteStatus;
            set
            {
                if (_GSDeleteStatus != value)
                {
                    _GSDeleteStatus = value;
                    RaisePropertyChanged("GSDeleteStatus");
                }
            }
        }

        /// <summary>
        /// Whether the transript can be saved.
        /// </summary>
        public bool CanSaveTranscript { get => TranscribingStatus == Status.Done; }

        /// <summary>
        /// The formatted time elapsed since transcribing began.
        /// </summary>
        private string _Timer = "";
        /// <summary>
        /// The formatted time elapsed since transcribing began.
        /// </summary>
        public string Timer 
        {
            get => _Timer;
            set
            {
                if (_Timer != value)
                {
                    _Timer = value;
                    RaisePropertyChanged("Timer");
                }
            }
        }

        public MainViewModel(MainWindow owner)
        {
            Owner = owner;
            Owner.DataContext = this;
        }

        public void Reset()
        {
            AudioFilePath = "";
            ResetStatus();
        }

        public void ResetStatus()
        {
            Transcript = "";
            IsWorking = false;
            ReencodingStatus = Status.NotStarted;
            GSUploadStatus = Status.NotStarted;
            GSDeleteStatus = Status.NotStarted;
            TranscribingStatus = Status.NotStarted;
            Timer = "";
            _ReencodingProgress = "";
            _GSUploadProgress = "";
        }

        public async Task Transcribe()
        {
            ResetStatus();
            IsWorking = true;

            var stopwatch = new Stopwatch();
            var timer = new Timer((e) => { Timer = FormatTime(stopwatch.Elapsed); }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            stopwatch.Start();

            TranscribableAudioFile transcribableAudioFile;

            try
            {
                transcribableAudioFile = new TranscribableAudioFile(
                    AudioFilePath,
                    SpeakersCount,
                    Properties.Settings.Default.GoogleStorageBucketName,
                    Properties.Settings.Default.GoogleCredentialsFilePath
                );

                transcribableAudioFile.Reencoding += (object sender, EventArgs e) =>
                {
                    ReencodingStatus = Status.Pending;
                    ReencodingProgress = Math.Round(transcribableAudioFile.ReencodingProgress, 0) + " %";
                };

                transcribableAudioFile.Reencoded += (object sender, EventArgs e) =>
                {
                    ReencodingStatus = Status.Done;
                };

                transcribableAudioFile.GSUploading += (object sender, EventArgs e) =>
                {
                    GSUploadStatus = Status.Pending;
                    GSUploadProgress = Math.Round(transcribableAudioFile.GSUploadProgress, 0) + " %";
                };

                transcribableAudioFile.GSUploaded += (object sender, EventArgs e) =>
                {
                    GSUploadStatus = Status.Done;
                };

                transcribableAudioFile.GSDeleting += (object sender, EventArgs e) =>
                {
                    GSDeleteStatus = Status.Pending;
                };

                transcribableAudioFile.GSDeleted += (object sender, EventArgs e) =>
                {
                    GSDeleteStatus = Status.Done;
                };

                transcribableAudioFile.Recognizing += (object sender, EventArgs e) =>
                {
                    TranscribingStatus = Status.Pending;
                };

                transcribableAudioFile.Recognized += (object sender, EventArgs e) =>
                {
                    Transcript = transcribableAudioFile.Transcript;
                    TranscribingStatus = Status.Done;
                    IsWorking = false;
                };

                await transcribableAudioFile.Transcribe();
            }
            catch (Exception e)
            {
                if (ReencodingStatus == Status.Pending)
                {
                    ReencodingStatus = Status.Error;
                }
                if (GSUploadStatus == Status.Pending)
                {
                    GSUploadStatus = Status.Error;
                }
                if (GSDeleteStatus == Status.Pending)
                {
                    GSDeleteStatus = Status.Error;
                }
                if (TranscribingStatus == Status.Pending)
                {
                    TranscribingStatus = Status.Error;
                }

                var message = e.Message;
                if (e.InnerException != null)
                {
                    message += "\n" + e.InnerException.Message;
                }

                MessageBox.Show(Owner, message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetStatus();
            }
            finally
            {
                timer.Dispose();
                stopwatch.Stop();
                transcribableAudioFile = null;
            }
        }

        public void Save(string path)
        {
            File.WriteAllText(path, Transcript);
        }

        /// <summary>
        /// Formats a given time span.
        /// </summary>
        /// <param name="time">The time span to format.</param>
        /// <returns>The formatted time span.</returns>
        private string FormatTime(TimeSpan time)
        {
            if (time.Hours > 0)
            {
                return String.Format("{0:0}:{1:00}:{2:00}", time.Hours, time.Minutes, time.Seconds);
            }

            if (time.Minutes > 0)
            {
                return String.Format("{0:0}:{1:00}", time.Minutes, time.Seconds);
            }

            return String.Format("{0:0}", time.Seconds);
        }

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
