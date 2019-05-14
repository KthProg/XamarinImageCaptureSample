using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.Views;
using XamarinImageCaptureSample.Services;
using XamarinImageCaptureSample.Droid.Services;
using Xamarin.Forms;
using System.Threading.Tasks;
using global::Android.Gms.Vision.Texts;
using Java.Nio;
using Android.Graphics;
using Android.Util;
using Android.Gms.Vision.Barcodes;
using Android.App;

[assembly: Dependency(typeof(LabelReader))]
namespace XamarinImageCaptureSample.Droid.Services {
    public class LabelReader : LabelReaderBase, ILabelReader {
        private static readonly string LogTag = nameof(LabelReader);

        /// <summary>
        /// The android object for recognizing text blocks in a document
        /// </summary>
        private TextRecognizer TextRecognizer;

        /// <summary>
        /// The android object for recognizing QR codes
        /// </summary>
        private BarcodeDetector BarcodeDetector;

        /// <summary>
        /// The maximum angle between two text blocks before we consider them
        /// to be on separate lines.
        /// </summary>
        private const double MaxAngleBetweenLines = Math.PI / 16;

        private bool _initialized = false;
        public override bool Initialized => _initialized;

        /// <summary>
        /// Initializes the text recognizer.
        /// </summary>
        public LabelReader() {
            
        }

        public override void Init() {
            if (Initialized) { return; }
            _initialized = true;
            TextRecognizer = new TextRecognizer.Builder(Android.App.Application.Context).Build();
            BarcodeDetector = new BarcodeDetector.Builder(Android.App.Application.Context).SetBarcodeFormats(BarcodeFormat.QrCode).Build();
        }

        /// <summary>
        /// <see cref="LabelReaderBase.GetFullTextFromImage(object)"/>
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public override Task<string> GetFullTextFromImageAsync(object image) {
            byte[] imageBytes = (byte[])image;
            TaskCompletionSource<string> taskCompletionSource = new TaskCompletionSource<string>();
            StringBuilder stringBuilder = new StringBuilder();
            using (Bitmap imageBitmap = BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length)) 
            using (global::Android.Gms.Vision.Frame frame = new global::Android.Gms.Vision.Frame.Builder().SetBitmap(imageBitmap).Build()) {
                SparseArray textArray = TextRecognizer.Detect(frame);
                List<string> valuesInOrder = OrderTextBlocks(textArray);
                foreach (string value in valuesInOrder) {
                    stringBuilder.Append(value);
                }
                taskCompletionSource.SetResult(stringBuilder.ToString());
            }
            return taskCompletionSource.Task;
        }



        /// <summary>
        /// Given a list of text blocks, orders them by line, and
        /// from left to right within each line. The lines themselves are
        /// in no particular order.
        /// </summary>
        /// <param name="textBlocks"></param>
        /// <returns></returns>
        private static List<string> OrderTextBlocks(SparseArray textBlocks) {
            List<TextBlock> blocks = new List<TextBlock>();
            for (int i = 0; i < textBlocks.Size(); ++i) {
                TextBlock text = (TextBlock)textBlocks.ValueAt(i);
                blocks.Add(text);
            }

            List<string> results = new List<string>();

            while (blocks.Any()) {
                TextBlock blockToExamine = blocks.First();
                // get items in this line
                List<TextBlock> allInThisLine = GetAllInLine(blocks, blockToExamine);
                // order from left to right
                List<TextBlock> orderedInLine = allInThisLine.OrderBy(block => block.BoundingBox.Left).ToList();
                foreach(TextBlock block in orderedInLine) {
                    results.Add(block.Value);
                    results.Add(" ");
                }
                results.Add(System.Environment.NewLine);
                // remove items in this line before we check the next line
                blocks.RemoveAll((text) => allInThisLine.Contains(text));
            }

            return results;
        }

        /// <summary>
        /// Gets all text blocks in the same line as
        /// the given text block based on a maximum angle from
        /// their centers.
        /// </summary>
        /// <param name="all"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        private static List<TextBlock> GetAllInLine(List<TextBlock> all, TextBlock start) {
            return all.Where(block => {
                double angle = GetAngleBetween(start.BoundingBox, block.BoundingBox);
                return angle <= MaxAngleBetweenLines;
            }).ToList();
        }

        /// <summary>
        /// Gets the angle between the center of two rectangles,
        /// an essential step in figuring out what text blocks are on
        /// each line.
        /// </summary>
        /// <param name="rect1"></param>
        /// <param name="rect2"></param>
        /// <returns></returns>
        private static double GetAngleBetween(Rect rect1, Rect rect2) {
            return Math.Abs(Math.Atan2(rect1.CenterY() - rect2.CenterY(), rect1.CenterX() - rect2.CenterX()));
        }

        /// <summary>
        /// <see cref=ILabelReader.GetRawBarcodeTextFromImageAsync(object)"/>
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public override Task<string> GetRawBarcodeTextFromImageAsync(object image) {
            byte[] imageBytes = (byte[])image;
            TaskCompletionSource<string> taskCompletionSource = new TaskCompletionSource<string>();
            using (Bitmap imageBitmap = BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length))
            using (global::Android.Gms.Vision.Frame frame = new global::Android.Gms.Vision.Frame.Builder().SetBitmap(imageBitmap).Build()) {
                SparseArray barcodeArray = BarcodeDetector.Detect(frame);
                if(barcodeArray.Size() <= 0) {
                    taskCompletionSource.SetResult(String.Empty);
                } else {
                    Barcode qr = (Barcode)(barcodeArray.ValueAt(0));
                    taskCompletionSource.SetResult(qr.RawValue);
                }
            }
            return taskCompletionSource.Task;
        }

        public override void Dispose()
        {
            TextRecognizer.Release();
            BarcodeDetector.Release();
        }
    }
}