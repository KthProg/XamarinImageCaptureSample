using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace XamarinImageCaptureSample.Services {
    public abstract class LabelReaderBase : ILabelReader {
        /// <summary>
        /// <see cref="ILabelReader.Identifier"/>
        /// </summary>
        public string Text { get; protected set; }

        /// <summary>
        /// <see cref="ILabelReader.IsCompleted"/>
        /// </summary>
        public bool IsCompleted { get; protected set; }
        /// <summary>
        /// <see cref="ILabelReader.IsVerified"/>
        /// </summary>
        public bool IsVerified { get; protected set; }

        public abstract bool Initialized { get; }

        /// <summary>
        /// <see cref="ILabelReader.BuildFromText(string)"/>
        /// </summary>
        /// <param name="annotationFullText"></param>
        /// <returns></returns>
        public bool BuildFromText(string annotationFullText) {
            // TODO: actually process text as per your needs
            IsVerified = (Text == annotationFullText) && IsCompleted;
            Text = annotationFullText;
            IsCompleted = true;
            return true;
        }

        public abstract Task<string> GetFullTextFromImageAsync(object image);
        public abstract Task<string> GetRawBarcodeTextFromImageAsync(object image);

        /// <summary>
        /// <see cref="ILabelReader.BuildFromQR(string)"/>
        /// </summary>
        /// <param name="qrRawValue"></param>
        /// <returns></returns>
        public bool BuildFromQR(string qrRawValue) {
            // TODO: actually process text as per your needs
            IsVerified = (Text == qrRawValue) && IsCompleted;
            Text = qrRawValue;
            IsCompleted = true;
            return true;
        }

        public abstract void Init();
        public abstract void Dispose();
    }
}
