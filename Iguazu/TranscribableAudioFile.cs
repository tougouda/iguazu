using Google.Apis.Upload;
using Google.Cloud.Speech.V1;
using Google.Cloud.Storage.V1;
using Google.LongRunning;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xabe.FFmpeg;

namespace Iguazu
{
    /// <summary>
    /// Represents an audio file to transcribe.
    /// </summary>
    class TranscribableAudioFile
    {
        #region Constants
        /// <summary>
        /// The environment variable where to store the Google credentials file path.
        /// </summary>
        private const string GOOGLE_CREDENTIALS_ENVAR = "GOOGLE_APPLICATION_CREDENTIALS";

        /// <summary>
        /// The default language code.
        /// </summary>
        private const string DEFAULT_LANGUAGE = LanguageCodes.French.France;

        /// <summary>
        /// The Google Storage URI pattern.
        /// </summary>
        private const string GS_URI_PATTERN = "gs://{0}/{1}";
        #endregion

        #region Properties
        /// <summary>
        /// The source audio file path.
        /// </summary>
        public string Path { get; }
        /// <summary>
        /// The reencoded audio file path.
        /// </summary>
        public string ReencodedPath { get; private set; }
        /// <summary>
        /// The audio file speakers count.
        /// </summary>
        public int SpeakersCount { get; set; }
        /// <summary>
        /// The Google Storage bucket where to upload the file.
        /// </summary>
        public string Bucket { get; }
        /// <summary>
        /// The audio content language.
        /// </summary>
        public string Language { get; }
        /// <summary>
        /// The audio file transcript.
        /// </summary>
        public string Transcript { get; private set; } = null;
        /// <summary>
        /// The Google Storage URI of the uploaded file.
        /// </summary>
        private string GSUri { get; set; }
        /// <summary>
        /// The audio file channels counts.
        /// </summary>
        private int Channels { get; } = 1;
        /// <summary>
        /// The audio file sample rate.
        /// </summary>
        private int SampleRate { get; } = 48000;
        /// <summary>
        /// The audio file reencoding progression.
        /// </summary>
        public float ReencodingProgress { get; private set; } = 0;
        /// <summary>
        /// The Google Storage upload progression.
        /// </summary>
        public float GSUploadProgress { get; private set; } = 0;
        #endregion

        /// <summary>
        /// Initializes a new instance of the TranscribableAudioFile class.
        /// </summary>
        /// <param name="path">The audio file path.</param>
        /// <param name="speakersCount">The audio file speakers count.</param>
        /// <param name="bucket">The Google Storage bucket where to upload the file.</param>
        /// <param name="credentialsPath">The Google credentials file path.</param>
        /// <param name="language">The audio content language.</param>
        public TranscribableAudioFile(string path, int speakersCount, string bucket, string credentialsPath, string language)
        {
            Path = path;
            SpeakersCount = speakersCount;
            Bucket = bucket;
            Language = language;
            Environment.SetEnvironmentVariable(GOOGLE_CREDENTIALS_ENVAR, credentialsPath, EnvironmentVariableTarget.Process);
        }

        /// <summary>
        /// Initializes a new instance of the TranscribableAudioFile class.
        /// </summary>
        /// <param name="path">The audio file path.</param>
        /// <param name="bucket">The Google Storage bucket where to upload the file.</param>
        /// <param name="credentialsPath">The Google credentials file path.</param>
        public TranscribableAudioFile(string path, int speakersCount, string bucket, string credentialsPath) : this(path, speakersCount, bucket, credentialsPath, DEFAULT_LANGUAGE) { }

        /// <summary>
        /// Transcribe the audio file content.
        /// </summary>
        public async Task Transcribe()
        {
            await Reencode();

            if (ReencodedPath != null)
            {
                await GSUpload();

                TranscriptException exception = null;

                if (GSUri != null)
                {
                    try
                    {
                        await Recognize();
                    }
                    catch (TranscriptException e)
                    {
                        exception = e;
                    }

                    await GSDelete();
                }

                File.Delete(ReencodedPath);
                ReencodedPath = "";

                if (exception != null)
                {
                    throw exception;
                }
            }
        }

        /// <summary>
        /// Reencodes the audio file.
        /// </summary>
        private async Task Reencode()
        {
            if (!File.Exists(Path))
            {
                throw new FileNotFoundException("The file does not exists.");
            }

            FFmpeg.SetExecutablesPath(AppDomain.CurrentDomain.BaseDirectory + System.IO.Path.DirectorySeparatorChar + "Tools");

            ReencodedPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".flac");

            OnReencoding(EventArgs.Empty);

            try
            {
                IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(Path);

                IStream audioStream = mediaInfo.AudioStreams.FirstOrDefault()
                    ?.SetCodec(AudioCodec.flac)
                    ?.SetSampleRate(SampleRate)
                    ?.SetChannels(Channels);

                var conversion = FFmpeg.Conversions.New();
                conversion.AddStream(audioStream)
                    .AddParameter("-sample_fmt s16")
                    .SetOutput(ReencodedPath);

                conversion.OnProgress += (sender, args) =>
                {
                    ReencodingProgress = (float)(args.Duration.TotalSeconds / args.TotalLength.TotalSeconds * 100);
                    OnReencoding(EventArgs.Empty);
                };

                await conversion.Start();

                OnReencoded(EventArgs.Empty);
            }
            catch (Exception e)
            {
                throw new ReencodingException("An error occured while reencoding the audio file.", e);
            }
        }

        /// <summary>
        /// Gets the Google Storage URI from an object name.
        /// </summary>
        /// <param name="bucketName">The Google Storage bucket name.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>The Google Storage URI.</returns>
        private static string GetGSUriFromObjectName(string bucketName, string objectName)
        {
            return String.Format(GS_URI_PATTERN, bucketName, objectName);
        }

        /// <summary>
        /// Gets the object name from a Google Storage URI.
        /// </summary>
        /// <param name="GSUri">The Google Storage URI.</param>
        /// <returns>The object name.</returns>
        private static string GetObjectNameFromGSUri(string GSUri)
        {
            return GSUri.Substring(GSUri.LastIndexOf('/') + 1);
        }

        /// <summary>
        /// Uploads the file onto Google Storage.
        /// </summary>
        private async Task GSUpload()
        {
            OnGSUploading(EventArgs.Empty);

            try
            {
                var client = StorageClient.Create();
                var objectName = System.IO.Path.GetFileName(ReencodedPath);
                var contentType = "text/plain";

                var options = new UploadObjectOptions();
                // Create a temporary uploader so the upload session can be manually initiated without actually uploading.
                var tempUploader = client.CreateObjectUploader(Bucket, objectName, contentType, new MemoryStream(), options);
                var uploadUri = await tempUploader.InitiateSessionAsync();

                // Send uploadUri to (unauthenticated) client application, so it can perform the upload:
                using (var stream = File.OpenRead(ReencodedPath))
                {
                    IProgress<IUploadProgress> progress = new Progress<IUploadProgress>(
                      p => { GSUploadProgress = (float)p.BytesSent / stream.Length * 100; OnGSUploading(EventArgs.Empty); }
                    );

                    var actualUploader = ResumableUpload.CreateFromUploadUri(uploadUri, stream);
                    actualUploader.ProgressChanged += progress.Report;
                    actualUploader.ChunkSize = ResumableUpload.MinimumChunkSize * 4;
                    await actualUploader.UploadAsync();
                }

                GSUri = GetGSUriFromObjectName(Bucket, objectName);
                OnGSUploaded(EventArgs.Empty);
            }
            catch (Exception e)
            {
                throw new GSUploadException("An error occured while uploading the file.", e);
            }
        }

        /// <summary>
        /// Deletes the file from Google Storage.
        /// </summary>
        private async Task GSDelete()
        {
            if (GSUri != null)
            {
                OnGSDeleting(EventArgs.Empty);

                var objectName = GetObjectNameFromGSUri(GSUri);

                try
                {
                    var storage = await StorageClient.CreateAsync();
                    await storage.DeleteObjectAsync(Bucket, objectName);

                    OnGSDeleted(EventArgs.Empty);
                }
                catch (Exception e)
                {
                    throw new GSDeleteException("An error occured while deleting distant file.", e);
                }
            }
        }

        /// <summary>
        /// Performs an asynchronous speech recognition of the uploaded file.
        /// </summary>
        private async Task Recognize()
        {
            OnRecognizing(EventArgs.Empty);

            try
            {
                var speech = await SpeechClient.CreateAsync();

                LongRunningRecognizeRequest request = new LongRunningRecognizeRequest
                {
                    Config = new RecognitionConfig()
                    {
                        Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
                        AudioChannelCount = Channels,
                        SampleRateHertz = SampleRate,
                        LanguageCode = Language,
                        EnableAutomaticPunctuation = true,
                        DiarizationConfig = new SpeakerDiarizationConfig()
                        {
                            EnableSpeakerDiarization = true,
                            MinSpeakerCount = SpeakersCount,
                            MaxSpeakerCount = SpeakersCount
                        }
                    },
                    Audio = RecognitionAudio.FromStorageUri(GSUri)
                };

                Operation<LongRunningRecognizeResponse, LongRunningRecognizeMetadata> response = await speech.LongRunningRecognizeAsync(request);
                Operation<LongRunningRecognizeResponse, LongRunningRecognizeMetadata> completedResponse = await response.PollUntilCompletedAsync();

                var text = "";

                var result = completedResponse.Result.Results;

                if (result.Count > 0)
                {
                    var alternative = result.ElementAt(result.Count - 1).Alternatives[0];
                    int currentSpeakerTag = 0;

                    for (var i = 0; i < alternative.Words.Count; i++)
                    {
                        var wordInfo = alternative.Words[i];
                        if (currentSpeakerTag == wordInfo.SpeakerTag)
                        {
                            text += $" {wordInfo.Word}";
                        }
                        else
                        {
                            if (text != "")
                            {
                                text += "\n";
                            }
                            if (SpeakersCount > 1)
                            {
                                text += $"- Intervenant {wordInfo.SpeakerTag} : ";
                            }
                            text += wordInfo.Word;
                            currentSpeakerTag = wordInfo.SpeakerTag;
                        }
                    }
                }

                Transcript = text;

                #if DEBUG
                Transcript += "\n\n" + result.ToString();
                #endif

                OnRecognized(EventArgs.Empty);
            }
            catch (Exception e)
            {
                throw new TranscriptException("An error occured during transcription.", e);
            }
        }

        #region Events
        /// <summary>
        /// Fires an event.
        /// </summary>
        /// <param name="handler">The event handler.</param>
        /// <param name="e">The event arguments.</param>
        private void FireEvent(EventHandler handler, EventArgs e)
        {
            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Handles the event fired when the file is being reencoding.
        /// </summary>
        public event EventHandler Reencoding;
        /// <summary>
        /// Fires an event when the file is being reencoding.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnReencoding(EventArgs e)
        {
            FireEvent(Reencoding, e);
        }

        /// <summary>
        /// Handles the event fired when the file is reencoded.
        /// </summary>
        public event EventHandler Reencoded;
        /// <summary>
        /// Fires an event when the file is reencoded.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnReencoded(EventArgs e)
        {
            FireEvent(Reencoded, e);
        }

        /// <summary>
        /// Handles the event fired when the file is being uploaded onto Google Storage.
        /// </summary>
        public event EventHandler GSUploading;
        /// <summary>
        /// Fires an event when the file is being uploaded onto Google Storage.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnGSUploading(EventArgs e)
        {
            FireEvent(GSUploading, e);
        }

        /// <summary>
        /// Handles the event fired when the file is uploaded onto Google Storage.
        /// </summary>
        public event EventHandler GSUploaded;
        /// <summary>
        /// Fires an event when the file is uploaded onto Google Storage.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnGSUploaded(EventArgs e)
        {
            FireEvent(GSUploaded, e);
        }

        /// <summary>
        /// Handles the event fired when the file is being deleted from Google Storage.
        /// </summary>
        public event EventHandler GSDeleting;
        /// <summary>
        /// Fires an event when the file is being deleted from Google Storage.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnGSDeleting(EventArgs e)
        {
            FireEvent(GSDeleting, e);
        }

        /// <summary>
        /// Handles the event fired when the file is deleted from Google Storage.
        /// </summary>
        public event EventHandler GSDeleted;
        /// <summary>
        /// Fires an event when the file is deleted from Google Storage.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnGSDeleted(EventArgs e)
        {
            FireEvent(GSDeleted, e);
        }

        /// <summary>
        /// Handles the event fired when the file content is being recognized.
        /// </summary>
        public event EventHandler Recognizing;
        /// <summary>
        /// Fires an event when the file content is being recognized.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnRecognizing(EventArgs e)
        {
            FireEvent(Recognizing, e);
        }

        /// <summary>
        /// Handles the event fired when the file content is recognized.
        /// </summary>
        public event EventHandler Recognized;
        /// <summary>
        /// Fires an event when the file content is recognized.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnRecognized(EventArgs e)
        {
            FireEvent(Recognized, e);
        }
        #endregion

        #region Exceptions
        /// <summary>
        /// The exception that is thrown when the file does not exist. 
        /// </summary>
        class FileNotFoundException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the FileNotFoundException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            /// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not null, the current exception is raised in a catch block that handles the inner exception.</param>
            public FileNotFoundException(string message, Exception innerException = null) : base(message, innerException) { }
            /// <summary>
            /// Initializes a new instance of the FileNotFoundException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            public FileNotFoundException(string message) : this(message, null) { }
        }

        /// <summary>
        /// The exception that is thrown when the file media content is not valid. 
        /// </summary>
        class InvalidMediaException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the InvalidMediaException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            /// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not null, the current exception is raised in a catch block that handles the inner exception.</param>
            public InvalidMediaException(string message, Exception innerException = null) : base(message, innerException) { }
            /// <summary>
            /// Initializes a new instance of the InvalidMediaException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            public InvalidMediaException(string message) : this(message, null) { }
        }

        /// <summary>
        /// The exception that is thrown when an error occurs while trying to get media info. 
        /// </summary>
        class MediaInfoException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the MediaInfoException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            /// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not null, the current exception is raised in a catch block that handles the inner exception.</param>
            public MediaInfoException(string message, Exception innerException = null) : base(message, innerException) { }
            /// <summary>
            /// Initializes a new instance of the MediaInfoException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            public MediaInfoException(string message) : base(message, null) { }
        }

        /// <summary>
        /// The exception that is thrown when an error occurs while reencoding the audio file. 
        /// </summary>
        class ReencodingException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the ReencodingException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            /// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not null, the current exception is raised in a catch block that handles the inner exception.</param>
            public ReencodingException(string message, Exception innerException = null) : base(message, innerException) { }
            /// <summary>
            /// Initializes a new instance of the ReencodingException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            public ReencodingException(string message) : base(message, null) { }
        }

        /// <summary>
        /// The exception that is thrown when an error occurs while trying to upload the file onto Google Storage. 
        /// </summary>
        class GSUploadException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the UploadException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            /// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not null, the current exception is raised in a catch block that handles the inner exception.</param>
            public GSUploadException(string message, Exception innerException = null) : base(message, innerException) { }
            /// <summary>
            /// Initializes a new instance of the UploadException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            public GSUploadException(string message) : base(message, null) { }
        }

        /// <summary>
        /// The exception that is thrown when an error occurs while trying to delete the file from Google Storage. 
        /// </summary>
        class GSDeleteException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the DeleteDistantException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            /// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not null, the current exception is raised in a catch block that handles the inner exception.</param>
            public GSDeleteException(string message, Exception innerException = null) : base(message, innerException) { }
            /// <summary>
            /// Initializes a new instance of the DeleteDistantException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            public GSDeleteException(string message) : base(message, null) { }
        }

        /// <summary>
        /// The exception that is thrown when an error occurs while transcribing the file. 
        /// </summary>
        class TranscriptException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the TranscriptException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            /// <param name="innerException">The exception that is the cause of the current exception. If the innerException parameter is not null, the current exception is raised in a catch block that handles the inner exception.</param>
            public TranscriptException(string message, Exception innerException = null) : base(message, innerException) { }
            /// <summary>
            /// Initializes a new instance of the TranscriptException class.
            /// </summary>
            /// <param name="message">The message that describes the exception. The caller of this constructor is required to ensure that this string has been localized for the current system culture.</param>
            public TranscriptException(string message) : base(message, null) { }
        }
        #endregion
    }
}
