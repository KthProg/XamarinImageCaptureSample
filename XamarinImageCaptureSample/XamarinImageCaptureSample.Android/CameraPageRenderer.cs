using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::Android;
using global::Android.App;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.Graphics;
using global::Android.Support.V4.App;
using global::Android.Support.V4.Content;
using global::Android.Views;
using global::Android.Widget;
using global::Android.Hardware.Camera2;
using Xamarin.Forms.Platform.Android;
using Android.Runtime;
using Java.Nio;
using Android.Media;
using System.IO;
using Android.Hardware.Camera2.Params;
using Android.Content.Res;
using System.Threading;
using XamarinImageCaptureSample.Views;
using XamarinImageCaptureSample.Droid;

[assembly: Xamarin.Forms.ExportRenderer(typeof(LabelReader), typeof(CameraPageRenderer))]
namespace XamarinImageCaptureSample.Droid
{
    /// <summary>
    /// Platform-specific implementation of a page which displays a camera preview, and takes
    /// a picture of the image, processing it through the <see cref="LabelReader"/> view.
    /// </summary>
    public class CameraPageRenderer : PageRenderer, TextureView.ISurfaceTextureListener {
        Activity CurrentContext => Context.GetActivity();
        /// <summary>
        /// The callback handler for a new camera session.
        /// </summary>
        public CameraCaptureSessionCallback SessionCallback { get; set; }
        /// <summary>
        /// The callback handler for a new camera capture.
        /// </summary>
        public CameraCaptureListener CaptureListener { get; set; }
        /// <summary>
        /// The callback handler for when a new image is
        /// available for the image reader.
        /// </summary>
        public CameraImageListener CameraImageReaderListener { get; set; }
        /// <summary>
        /// The image reader that processes new camera images.
        /// </summary>
        public ImageReader Reader { get; set; }
        /// <summary>
        /// The surface on which the preview is shown.
        /// </summary>
        public SurfaceTexture Surface { get; set; }
        /// <summary>
        /// The texture on which the preview is shown.
        /// </summary>
        public TextureView LiveView { get; set; }

        /// <summary>
        /// The callback handler for camera state changes.
        /// </summary>
        public CameraStateCallback StateCallback { get; set; }
        /// <summary>
        /// The request builder for requesting the camera session.
        /// </summary>
        public CaptureRequest.Builder Builder { get; set; }
        /// <summary>
        /// The active camera session.
        /// </summary>
        public CameraCaptureSession Session { get; set; }
        /// <summary>
        /// The active capture request
        /// </summary>
        public CaptureRequest Request { get; set; }
        /// <summary>
        /// The active camera device.
        /// </summary>
        public CameraDevice Camera { get; set; }

        public CameraPageOrientationEventListener OrientationEventListener { get; set; }

        public CameraPageRenderer(Context context) : base(context) {
            cameraManager = (CameraManager)Context.GetSystemService(Context.CameraService);
            windowManager = Context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            StateCallback = new CameraStateCallback(this);
            SessionCallback = new CameraCaptureSessionCallback(this);
            CaptureListener = new CameraCaptureListener(this);
            CameraImageReaderListener = new CameraImageListener(this);
            OrientationEventListener = new CameraPageOrientationEventListener(this, Context, global::Android.Hardware.SensorDelay.Normal);
        }

        /// <summary>
        /// Listens for orientation changes, and updates the transform on the preview
        /// texture when the landscape mode changes.
        /// </summary>
        public class CameraPageOrientationEventListener : OrientationEventListener {
            private SurfaceOrientation _lastOrientation;
            private readonly CameraPageRenderer _renderer;
            public CameraPageOrientationEventListener(CameraPageRenderer renderer, Context context, global::Android.Hardware.SensorDelay sensorDelay) : base(context, sensorDelay) {
                _renderer = renderer;
                _lastOrientation = renderer.windowManager.DefaultDisplay.Rotation;
            }

            public override void OnOrientationChanged(int orientation) {
                if(_renderer.windowManager.DefaultDisplay.Rotation != _lastOrientation) {
                    _lastOrientation = _renderer.windowManager.DefaultDisplay.Rotation;
                    _renderer.SetupPreviewMatrix();
                }
            }
        }

        float sensorOrientation;

        /// <summary>
        /// The text describing the action the user should take with the camera.
        /// </summary>

        TextView imageCaptureMessage;

        /// <summary>
        /// The outer layout that fills the view.
        /// </summary>
        RelativeLayout mainLayout;
        /// <summary> 
        /// The manager class for camera functionality.
        /// </summary>
        CameraManager cameraManager;

        /// <summary>
        /// The manager class for window layout stuffs
        /// </summary>
        IWindowManager windowManager;

        /// <summary>
        /// The current activity.
        /// </summary>
        Activity Activity => this.Context as Activity;

        /// <summary>
        /// The cancellation token source for canceling tasks run in the background
        /// </summary>
        CancellationTokenSource cancellationTokenSource;
        /// <summary>
        /// The cancellation token for canceling tasks run in the background
        /// </summary>
        CancellationToken CancellationToken { get; set; }

        protected override void OnElementChanged(ElementChangedEventArgs<Xamarin.Forms.Page> e) {
            base.OnElementChanged(e);
            SetupUserInterface();
            SetupEventHandlers();
        }

        /// <summary>
        /// Create the UI elements for the user interface.
        /// </summary>
        void SetupUserInterface() {
            mainLayout = new RelativeLayout(Context);

            LiveView = new TextureView(Context);

            RelativeLayout.LayoutParams matchParentParams = new RelativeLayout.LayoutParams(
                RelativeLayout.LayoutParams.MatchParent,
                RelativeLayout.LayoutParams.MatchParent);

            LiveView.LayoutParameters = matchParentParams;

            mainLayout.AddView(LiveView);

            imageCaptureMessage = new TextView(Context) {
                Text = LabelReaderConstants.PhotoCaptureInstructions,
                TextAlignment = TextAlignment.Center,
                Gravity = GravityFlags.Center
            };

            imageCaptureMessage.SetTextColor(Color.White);

            mainLayout.AddView(imageCaptureMessage);

            AddView(mainLayout);
        }

        protected override void OnLayout(bool changed, int l, int t, int r, int b) {
            base.OnLayout(changed, l, t, r, b);
            if (!changed)
                return;

            int width = r - l;
            int height = b - t;

            var msw = MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly);
            var msh = MeasureSpec.MakeMeasureSpec(height, MeasureSpecMode.Exactly);
            mainLayout.Measure(msw, msh);
            mainLayout.Layout(0, 0, width, height);

            imageCaptureMessage.SetX((mainLayout.Width - imageCaptureMessage.Width) / 2);
            imageCaptureMessage.SetY(imageCaptureMessage.Height / 2 + LabelReaderConstants.PhotoCaptureInstructionsMargin);

            SetupPreviewMatrix();
        }

        /// <summary>
        /// Sets the transform of the live preview to fill the screen and be aligned
        /// in the landscape direction.
        /// </summary>
        public void SetupPreviewMatrix() {
            float landscapeScreenRotation = 0.0f;
            if(windowManager.DefaultDisplay.Rotation == SurfaceOrientation.Rotation270) {
                landscapeScreenRotation = 180.0f;
            }

            float width = mainLayout.Width;
            float height = mainLayout.Height;

            Matrix matrix = new Matrix();
            matrix.PostRotate(360.0f - landscapeScreenRotation - sensorOrientation, width / 2.0f, height / 2.0f);
            if (sensorOrientation != 180) {
                matrix.PostScale(width / height, height / width, width / 2.0f, height / 2.0f);
            }
            LiveView.SetTransform(matrix);
        }

        /// <summary>
        /// Sets up event handlers for UI elements.
        /// </summary>
        public void SetupEventHandlers() {
            LiveView.SurfaceTextureListener = this;
        }
        public override bool OnKeyDown(Keycode keyCode, KeyEvent e) {
            // cancel capturing the camera output when the user goes back
            if (keyCode == Keycode.Back) {
                (Element as LabelReader)?.Cancel();
                return false;
            }
            return base.OnKeyDown(keyCode, e);
        }
        /// <summary>
        /// Stops the live preview, and requests a still capture from the camera session.
        /// When the still capture is handled, the live preview is started again.
        /// </summary>
        public void CaptureImage() {
            if (CancellationToken.IsCancellationRequested) { return; }
            CaptureRequest.Builder builder = Camera.CreateCaptureRequest(CameraTemplate.StillCapture);
            builder.AddTarget(Reader.Surface);
            Session.Capture(builder.Build(), CaptureListener, null);
        }

        #region TextureView.ISurfaceTextureListener implementations
        /// <summary>
        /// Sets up the camera, and requests permissions if needed.
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height) {
            Activity.RequestedOrientation = ScreenOrientation.SensorLandscape;
            OrientationEventListener.Enable();
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken = cancellationTokenSource.Token;

            Surface = surface;

            // if the camera permission has to be accepted, then
            // the camera will be started when that happens.
            (CurrentContext as MainActivity).OnCameraAccepted += StartCamera;

            if (ContextCompat.CheckSelfPermission(CurrentContext, Manifest.Permission.Camera) != Permission.Granted) {
                ActivityCompat.RequestPermissions(CurrentContext, new string[] { Manifest.Permission.Camera }, 1);
            } else {
                StartCamera();
            }
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface) {
            try {
                Session.StopRepeating();
                Session.Close();
            } catch (Exception) {
                Console.WriteLine("Failed to close camera session.");
            }
            try
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
            catch (Exception)
            {

            }
            OrientationEventListener.Disable();
            try {
                (CurrentContext as MainActivity).OnCameraAccepted -= StartCamera;
            } catch (Exception) {
                Console.WriteLine("Failed to remove camera accepted handler. This likely means permissions were already accepted.");
            }
            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) {
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface) {
        }
        #endregion

        /// <summary>
        /// Gets the rear camera if possible, if not, then uses the front camera.
        /// </summary>
        /// <returns></returns>
        private string GetCameraIdForOrientation(LensFacing facingToMatch) {
            CameraCharacteristics characteristics = null;
            return cameraManager.GetCameraIdList().FirstOrDefault(id => {
                characteristics = cameraManager.GetCameraCharacteristics(id);
                int lensFacing = (int)characteristics.Get(CameraCharacteristics.LensFacing);
                return lensFacing == (int)facingToMatch;
            });
        }

        /// <summary>
        /// Finds the rear camera, and begins a session with it. Transforms the live view so that it
        /// is the correct orientation for the sensor/screen, and scales it to fit within its container.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public void StartCamera(object sender = null, EventArgs args = null) {
            string cameraId = 
                GetCameraIdForOrientation(LensFacing.Back) ?? 
                GetCameraIdForOrientation(LensFacing.Front) ?? 
                GetCameraIdForOrientation(LensFacing.External);

            if (cameraId == null) {
                (Element as LabelReader)?.CameraError(LabelReaderConstants.NoCameraMessage);
                return;
            }

            CameraCharacteristics characteristics = cameraManager.GetCameraCharacteristics(cameraId);

            sensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);

            SetupPreviewMatrix();

            int bestWidth = 0;

            var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
            global::Android.Util.Size[] outputSizes = map.GetOutputSizes((int)ImageFormatType.Jpeg);
            IEnumerable<global::Android.Util.Size> bigSizes = outputSizes.Where(size => size.Width >= LabelReaderConstants.MinimumUsefulImageWidthPixels);
            if (!bigSizes.Any()) {
                bestWidth = outputSizes.Max(size => size.Width);
            } else {
                bestWidth = bigSizes.Min(size => size.Width);
            }

            global::Android.Util.Size bestSize = outputSizes.First(size => size.Width == bestWidth);

            Reader = ImageReader.NewInstance(bestSize.Width, bestSize.Height, ImageFormatType.Jpeg, 2);
            Reader.SetOnImageAvailableListener(CameraImageReaderListener, null);

            cameraManager.OpenCamera(cameraId, StateCallback, null);
        }

        /// <summary>
        /// Handler for the camera device state when opening a camera. Create the
        /// live preview capture session.
        /// </summary>
        public class CameraStateCallback : CameraDevice.StateCallback {
            private readonly CameraPageRenderer _renderer;
            public CameraStateCallback(CameraPageRenderer renderer) {
                _renderer = renderer;
            }

            public override void OnDisconnected(CameraDevice camera) {
                (_renderer.Element as LabelReader)?.CameraError(LabelReaderConstants.CameraDisconnected);
            }

            public override void OnError(CameraDevice camera, [GeneratedEnum] CameraError error) {
                (_renderer.Element as LabelReader)?.CameraError($"Camera error ({Enum.GetName(typeof(CameraError), error)})");
            }

            public override void OnOpened(CameraDevice camera) {
                // request a preview capture of the camera, and notify the session
                // that we will be rendering to the image reader, as well as the preview surface.
                _renderer.Camera = camera;
                var surface = new Surface(_renderer.Surface);
                _renderer.Builder = camera.CreateCaptureRequest(CameraTemplate.Preview);
                _renderer.Builder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
                _renderer.Builder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Start);
                _renderer.Builder.AddTarget(surface);
                camera.CreateCaptureSession(new List<Surface> { surface, _renderer.Reader.Surface }, _renderer.SessionCallback, null);
            }
        }

        /// <summary>
        /// Handler for camera session creation events. Creates the live preview request
        /// and begins capturing still images.
        /// </summary>
        public class CameraCaptureSessionCallback : CameraCaptureSession.StateCallback {
            private readonly CameraPageRenderer _renderer;
            public CameraCaptureSessionCallback(CameraPageRenderer renderer) {
                _renderer = renderer;
            }

            public override void OnConfigured(CameraCaptureSession session) {
                // set a repeating request for a live preview of the camera
                _renderer.Session = session;
                CaptureRequest request = _renderer.Builder.Build();
                _renderer.Request = request;
                session.SetRepeatingRequest(request, _renderer.CaptureListener, null);
                // wait a bit before we start capturing the label (while the user
                // finds it and aligns it)
                _renderer.CurrentContext.RunOnUiThread(async () => {
                    try {
                        await Task.Delay(LabelReaderConstants.ImageCaptureBeginDelayMilliseconds, _renderer.CancellationToken);
                    } catch (TaskCanceledException) {
                        return;
                    }
                    _renderer.CaptureImage();
                });
            }

            public override void OnConfigureFailed(CameraCaptureSession session) {
                (_renderer.Element as LabelReader)?.CameraError(LabelReaderConstants.FailedToConfigureSession);
            }
        }

        /// <summary>
        /// Handler for camera capture events. Notified when a still image or live preview
        /// image is captured from the session. Not currently used for anything but required
        /// for the camera2 api.
        /// </summary>
        public class CameraCaptureListener : CameraCaptureSession.CaptureCallback {
            private readonly CameraPageRenderer _renderer;
            public CameraCaptureListener(CameraPageRenderer renderer) {
                _renderer = renderer;
            }

            public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result) {

            }

            public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult) {
                
            }
        }

        /// <summary>
        /// Image listener for the still image capture. Notified when an image is available
        /// from the session. Will continuously trigger a capture image request every time it receives
        /// an image.
        /// </summary>
        public class CameraImageListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener {
            private readonly CameraPageRenderer _renderer;
            public CameraImageListener(CameraPageRenderer renderer) {
                _renderer = renderer;
            }

            public void OnImageAvailable(ImageReader reader) {
                if (_renderer.CancellationToken.IsCancellationRequested) { return; }
                // get the byte array data from the first plane
                // of the image. This is sufficient for a JPEG
                // image
                Image image = reader.AcquireLatestImage();
                if (image != null) {
                    Image.Plane[] planes = image.GetPlanes();
                    ByteBuffer buffer = planes[0].Buffer;
                    byte[] bytes = new byte[buffer.Capacity()];
                    buffer.Get(bytes);
                    // close the image so we can handle another image later
                    image.Close();
                    (_renderer.Element as LabelReader)?.ProcessPhoto(bytes);
                    // keeps capturing images until the annotations have
                    // been processed into ssid and password sucessfully
                    _renderer.CurrentContext.RunOnUiThread(async () => {
                        try { 
                           await Task.Delay(LabelReaderConstants.ImageCaptureDelayMilliseconds, _renderer.CancellationToken);
                        } catch (TaskCanceledException) {
                            return;
                        }
                        _renderer.CaptureImage();
                    });
                }
            }
        }
    }
}