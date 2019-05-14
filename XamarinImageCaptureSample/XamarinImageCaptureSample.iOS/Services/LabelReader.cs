using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Foundation;
using XamarinImageCaptureSample.Services;
using XamarinImageCaptureSample.iOS.Services;
using UIKit;
using Xamarin.Forms;
using System.Threading.Tasks;
using Firebase.MLKit.Vision;

[assembly: Dependency(typeof(LabelReader))]
namespace XamarinImageCaptureSample.iOS.Services {
    public class LabelReader : LabelReaderBase, ILabelReader {
        /// <summary>
        /// <see cref="LabelReaderBase.GetFullTextFromImage(object)"/>
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public override async Task<string> GetFullTextFromImageAsync(object image) {
            CoreMedia.CMSampleBuffer iOSImage = (CoreMedia.CMSampleBuffer)image;
            VisionImage visionImage = new VisionImage(iOSImage);
            visionImage.Metadata = new VisionImageMetadata {
                Orientation = GetOrientation()
            };
            VisionApi api = VisionApi.Create();
            VisionTextRecognizer textRecognizer = api.GetOnDeviceTextRecognizer();
            VisionText textResult = await textRecognizer.ProcessImageAsync(visionImage);
            return textResult?.Text;
        }

        public override bool Initialized => true;

        public override void Init() {

        }

        private static VisionDetectorImageOrientation GetOrientation() {
            switch (UIDevice.CurrentDevice.Orientation) {
                case UIDeviceOrientation.LandscapeLeft:
                    return VisionDetectorImageOrientation.TopLeft;
                case UIDeviceOrientation.LandscapeRight:
                    return VisionDetectorImageOrientation.BottomRight;
                default:
                    return VisionDetectorImageOrientation.BottomRight;
            }
        }

        /// <summary>
        /// <see cref="LabelReaderBase.GetRawBarcodeTextFromImageAsync(object)"/>
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public override async Task<string> GetRawBarcodeTextFromImageAsync(object image) {
            CoreMedia.CMSampleBuffer iOSImage = (CoreMedia.CMSampleBuffer)image;
            VisionImage visionImage = new VisionImage(iOSImage);
            visionImage.Metadata = new VisionImageMetadata {
                Orientation = GetOrientation()
            };
            VisionApi api = VisionApi.Create();
            VisionBarcodeDetector barcodeDetector = api.GetBarcodeDetector(new VisionBarcodeDetectorOptions(VisionBarcodeFormat.QRCode));
            VisionBarcode[] barcodes = await barcodeDetector.DetectAsync(visionImage);
            if(barcodes.Length <= 0) {
                return String.Empty;
            }
            return barcodes.First().RawValue;
        }

        public override void Dispose()
        {
            // believe it or not, nothign has to be disposed
        }
    }
}