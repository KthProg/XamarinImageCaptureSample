using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Xamarin.Forms.Platform.iOS;
using AVFoundation;
using UIKit;
using Foundation;
using System.Drawing;
using System.Threading;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using XamarinImageCaptureSample.Views;
using XamarinImageCaptureSample;
using XamarinImageCaptureSample.iOS;

[assembly: Xamarin.Forms.ExportRenderer(typeof(LabelReader), typeof(CameraPageRenderer))]
namespace XamarinImageCaptureSample.iOS {
    /// <summary>
    /// Platform-specific implementation of a page which displays a camera preview, and takes
    /// a picture of the image, processing it through the <see cref="LabelReader"/> view.
    /// </summary>
    public class CameraPageRenderer : PageRenderer, IAVCaptureVideoDataOutputSampleBufferDelegate {
        /// <summary>
        /// The session we have opened with the camera.
        /// </summary>
        AVCaptureSession captureSession;
        /// <summary>
        /// The camera input in our session.
        /// </summary>
        AVCaptureDeviceInput captureDeviceInput;
        /// <summary>
        /// The output class for frames from our camera session.
        /// </summary>
        AVCaptureVideoDataOutput videoDataOutput;
        /// <summary>
        /// The layer containing the video preview for still image capture
        /// </summary>
        AVCaptureVideoPreviewLayer videoPreviewLayer;
        /// <summary>
        /// The cancellation token source for canceling tasks run in the background
        /// </summary>
        CancellationTokenSource cancellationTokenSource;
        /// <summary>
        /// The cancellation token for canceling tasks run in the background
        /// </summary>
        CancellationToken cancellationToken;

        public CameraPageRenderer() : base() {

        }

        public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations() {
            return UIInterfaceOrientationMask.Landscape;
        }

        protected override void OnElementChanged(VisualElementChangedEventArgs e) {
            base.OnElementChanged(e);
            SetupUserInterface();
            SetupEventHandlers();
        }

        public override void WillAnimateRotation(UIInterfaceOrientation toInterfaceOrientation, double duration) {
            base.WillAnimateRotation(toInterfaceOrientation, duration);
            videoPreviewLayer.Connection.VideoOrientation = GetCaptureOrientation(toInterfaceOrientation);
        }

        public override async void ViewDidLoad() {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            base.ViewDidLoad();
            await AuthorizeCamera();
            SetupLiveCameraStream();
        }

        /// <summary>
        /// Gets authorization to access the camera.
        /// </summary>
        /// <returns></returns>
        async Task AuthorizeCamera() {
            var authStatus = AVCaptureDevice.GetAuthorizationStatus(AVMediaType.Video);
            if (authStatus != AVAuthorizationStatus.Authorized) {
                await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVMediaType.Video);
            }
        }

        /// <summary>
        /// Gets a useable camera for the orientation we require.
        /// </summary>
        /// <param name="orientation"></param>
        /// <returns></returns>
        public AVCaptureDevice GetCameraForOrientation(AVCaptureDevicePosition orientation) {
            var devices = AVCaptureDevice.DevicesWithMediaType(AVMediaType.Video);
            foreach (var device in devices) {
                if (device.Position == orientation) {
                    return device;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the orientation to capture the live preview image at
        /// based on the screen orientation. Always the nearest
        /// landscape mode.
        /// </summary>
        /// <returns></returns>
        private AVCaptureVideoOrientation GetCaptureOrientation(UIInterfaceOrientation orientation) {
            switch (orientation) {
                case UIInterfaceOrientation.LandscapeLeft:
                    return AVCaptureVideoOrientation.LandscapeLeft;
                case UIInterfaceOrientation.LandscapeRight:
                    return AVCaptureVideoOrientation.LandscapeRight;
                case UIInterfaceOrientation.Portrait:
                    return AVCaptureVideoOrientation.LandscapeLeft;
                case UIInterfaceOrientation.PortraitUpsideDown:
                    return AVCaptureVideoOrientation.LandscapeRight;
                default:
                    return AVCaptureVideoOrientation.LandscapeLeft;
            }
        }

        /// <summary>
        /// Starts a session with the camera, and creates the classes
        /// needed to view a video preview, and capture a still image.
        /// </summary>
        public void SetupLiveCameraStream() {
            captureSession = new AVCaptureSession() {
                SessionPreset = new NSString(AVCaptureSession.PresetHigh)
            };
            videoPreviewLayer = new AVCaptureVideoPreviewLayer(captureSession) {
                Frame = View.Frame,
                Orientation = GetCaptureOrientation(UIApplication.SharedApplication.StatusBarOrientation)
            };
            View.Layer.AddSublayer(videoPreviewLayer);

            AVCaptureDevice captureDevice = 
                GetCameraForOrientation(AVCaptureDevicePosition.Back) ?? 
                GetCameraForOrientation(AVCaptureDevicePosition.Front) ?? 
                GetCameraForOrientation(AVCaptureDevicePosition.Unspecified);

            if(captureDevice == null) {
                (Element as LabelReader).CameraError(LabelReaderConstants.NoCameraMessage);
                return;
            }

            captureDeviceInput = AVCaptureDeviceInput.FromDevice(captureDevice);
            captureSession.AddInput(captureDeviceInput);

            videoDataOutput = new AVCaptureVideoDataOutput();

            videoDataOutput.SetSampleBufferDelegateQueue(this, new CoreFoundation.DispatchQueue("frameQueue"));

            captureSession.AddOutput(videoDataOutput);
            captureSession.StartRunning();

            // set last processed time to now so the handler for video frames will wait an appropriate length of time
            // before processing images.
            lastImageProcessedTime = DateTime.Now;
        }

        /// <summary>
        /// Create the UI elements for the user interface.
        /// </summary>
        void SetupUserInterface() {
            // ui label with instructions is centered at the top.
            // to get it to appear at the top, the height must be adjusted to fit.
            // to accomplish this, I call SizeToFit, then set the frame to have
            // the same width as the screen, while preserving the height.
            UILabel takePhotoLabel = new UILabel();
            takePhotoLabel.Text = LabelReaderConstants.PhotoCaptureInstructions;
            int labelMargin = LabelReaderConstants.PhotoCaptureInstructionsMargin;
            takePhotoLabel.Frame = new CoreGraphics.CGRect(labelMargin, labelMargin, View.Frame.Width - labelMargin, View.Frame.Height - labelMargin);
            takePhotoLabel.BackgroundColor = ColorExtensions.ToUIColor(Color.Transparent);
            takePhotoLabel.TextColor = ColorExtensions.ToUIColor(Color.White);
            takePhotoLabel.TextAlignment = UITextAlignment.Center;
            takePhotoLabel.Lines = 0;
            takePhotoLabel.SizeToFit();
            takePhotoLabel.Frame = new CoreGraphics.CGRect(labelMargin, labelMargin, View.Frame.Width - labelMargin, takePhotoLabel.Frame.Height);

            View.AddSubview(takePhotoLabel);
        }

        /// <summary>
        /// Sets up event handlers for UI elements.
        /// </summary>
        void SetupEventHandlers() {

        }

        private bool imageProcessingStarted = false;
        private DateTime lastImageProcessedTime = DateTime.Now;

        [Export("captureOutput:didOutputSampleBuffer:fromConnection:")]
        public void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection) {
            if (!imageProcessingStarted) {
                if ((DateTime.Now - lastImageProcessedTime).TotalMilliseconds < LabelReaderConstants.ImageCaptureBeginDelayMilliseconds) { return; }
                imageProcessingStarted = true;
            }
            if((DateTime.Now - lastImageProcessedTime).TotalMilliseconds < LabelReaderConstants.ImageCaptureDelayMilliseconds) { return; }
            lastImageProcessedTime = DateTime.Now;
            (Element as LabelReader).ProcessPhoto(sampleBuffer);
        }

        public override void ViewDidUnload() {
            base.ViewDidUnload();
            try
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            } catch (Exception){}
            try { 
                captureDeviceInput.Dispose();
            } catch (Exception) { }
            try { 
                videoDataOutput.Dispose();
            } catch (Exception) { }
            try { 
                captureSession.StopRunning();
            } catch (Exception) { }
            try { 
                captureSession.Dispose();
            } catch (Exception) { }
        }
    }
}