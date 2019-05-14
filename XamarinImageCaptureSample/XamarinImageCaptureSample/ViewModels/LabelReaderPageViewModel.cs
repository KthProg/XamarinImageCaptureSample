using Acr.UserDialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Input;
using Xamarin.Forms;
using XamarinImageCaptureSample.Services;

namespace XamarinImageCaptureSample.ViewModels {
    /// <summary>
    /// A view model for a page that takes image captures of labels
    /// and annotates the text.
    /// </summary>
    public class LabelReaderPageViewModel {
        private BufferBlock<object> ImageQueue;
        private CancellationTokenSource CancellationTokenSource;
        private CancellationToken CancellationToken;

        private Task BackgroundOperation;

        private ILabelReader LabelReader;

        public string PhotoCaptureInstructions => LabelReaderConstants.PhotoCaptureInstructions;

        int TotalCaptures = 0;

        public LabelReaderPageViewModel() {
            LabelReader = DependencyService.Get<ILabelReader>(DependencyFetchTarget.NewInstance);
            TotalCaptures = 0;
            CancellationTokenSource = new CancellationTokenSource();
            CancellationToken = CancellationTokenSource.Token;
            ImageQueue = new BufferBlock<object>(new DataflowBlockOptions {
                BoundedCapacity = 1
            });
            BackgroundOperation = Task.Run(() => ProcessImageAsync(CancellationToken));
        }

        private void StopBackgroundOperations() {
            try
            {
                CancellationTokenSource.Cancel();
                CancellationTokenSource.Dispose();
            }
            catch (Exception)
            {

            }
        }

        private async void ProcessImageAsync(CancellationToken cancellationToken) {
            try {
                while (!cancellationToken.IsCancellationRequested) {
                    object image = await ImageQueue.ReceiveAsync(cancellationToken);
                    string fullText = await LabelReader.GetFullTextFromImageAsync(image);
                    string qrText = await LabelReader.GetRawBarcodeTextFromImageAsync(image);

                    try
                    {
                        (image as IDisposable)?.Dispose();
                    }
                    catch (Exception) { }

                    LabelReader.BuildFromText(fullText);
                    LabelReader.BuildFromQR(qrText);

                    if (LabelReader.IsCompleted) {
                        Console.WriteLine($"Label read result: {LabelReader.Text}");
                    }

                    ++TotalCaptures;

                    bool maxCapturesTaken = (TotalCaptures >= LabelReaderConstants.MaxCaptures);
                    if (LabelReader.IsVerified || maxCapturesTaken) {
                        UserDialogs.Instance.Toast(new ToastConfig($"Finished processing image after {TotalCaptures} captures"));
                        StopBackgroundOperations();
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"Exception while parsing label: {ex}");
                StopBackgroundOperations();
            } finally {
                ImageQueue.Complete();
                ImageQueue = null;
            }
        }

        /// <summary>
        /// A command that is executed when an error occurs with the camera.
        /// </summary>
        public ICommand CameraError => new Command((object message) => {
            string messageText = (string)message;
            UserDialogs.Instance.Toast(new ToastConfig(messageText));
        });

        /// <summary>
        /// A command that is executed when a photo is taken.
        /// </summary>
        public ICommand TakePhoto => new Command(async (object image) => {
            if (CancellationToken.IsCancellationRequested) { return; }
            // our activity has definitely loaded by now so we can initialize
            // if we haven't already (Android only)
            if (LabelReader != null && !LabelReader.Initialized) {
                LabelReader?.Init();
            }
            // receive any pending image(s), so that our background task will get the latest image
            // when it completes processing on the previous image
            IList<object> queuedData;
            ImageQueue.TryReceiveAll(out queuedData);
            queuedData = null;
            // force GC collect our unused byte arrays
            // so we don't overflow adding another
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await ImageQueue.SendAsync(image, CancellationToken);
        });
    }
}
