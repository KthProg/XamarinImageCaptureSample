using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace XamarinImageCaptureSample.Services {
    public interface ILabelReader : IDisposable {
        /// <summary>
        /// The current parsed text for the label reader
        /// </summary>
        string Text { get; }
        /// <summary>
        /// Whether or not the label reader has finished
        /// processing values for both the identifier and
        /// the password.
        /// </summary>
        bool IsCompleted { get; }
        /// <summary>
        /// Whether or not the processed values for identifier
        /// and password have been verified.
        /// </summary>
        bool IsVerified { get; }
        /// Gets the text from the image.
        /// </summary>
        /// <param name="image">The image</param>
        /// <returns>The text</returns>
        Task<string> GetFullTextFromImageAsync(object image);
        /// <summary>
        /// Gets the barcode text from the image.
        /// </summary>
        /// <param name="image">The image</param>
        /// <returns>The text</returns>
        Task<string> GetRawBarcodeTextFromImageAsync(object image);
        /// <summary>
        /// Builds the identifier and password from the
        /// given text if possible. True if both values
        /// were parsed.
        /// </summary>
        /// <param name="annotationFullText"></param>
        /// <returns></returns>
        bool BuildFromText(string annotationFullText);
        /// <summary>
        /// Builds the identifier and password from the
        /// given qr code if possible. True if both values
        /// were parsed.
        /// </summary>
        /// <param name="qrRawValue"></param>
        /// <returns></returns>
        bool BuildFromQR(string qrRawValue);
        /// <summary>
        /// Set the current activity. Needed to instantiate deprecated readers on Android.
        /// </summary>
        void Init();
        bool Initialized { get; }
    }
}
