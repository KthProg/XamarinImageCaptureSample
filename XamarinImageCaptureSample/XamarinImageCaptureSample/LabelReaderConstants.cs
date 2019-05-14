using System;
using System.Collections.Generic;
using System.Text;

namespace XamarinImageCaptureSample
{
    public static class LabelReaderConstants {
        public const string PhotoCaptureInstructions = "Align your label with the screen. Move the phone to a distance where the image is clear.";
        public const int PhotoCaptureInstructionsMargin = 5;
        public const int ImageCaptureDelayMilliseconds = 750;
        public const int MaxCaptures = 20;
        // the pixels we need to achieve 128 pixels per letter
        // when the letters take up 1/12 of the short side of the screen each.
        // 96 is fine for big labels, but for the smaller ones this works better
        public const int MinimumUsefulImageWidthPixels = 12 * 128;
        public const int ImageCaptureBeginDelayMilliseconds = 2500;
        public const string PhotoButtonText = "Take Photo";
        public const string NoCameraMessage = "No camera detected";
        public const string FailedToConfigureSession = "Failed to configure session with the camera";
        public const string CameraDisconnected = "Camera disconnected unexpectedly";
    }
}
