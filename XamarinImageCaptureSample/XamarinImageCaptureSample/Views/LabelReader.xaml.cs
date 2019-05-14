using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace XamarinImageCaptureSample.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class LabelReader : ContentPage
	{

        public LabelReader ()
		{
            InitializeComponent();
        }

        public static readonly BindableProperty TakePhotoCommandProperty =
            BindableProperty.Create(propertyName: nameof(TakePhotoCommand),
                                    returnType: typeof(ICommand),
                                    declaringType: typeof(LabelReader));

        public void ProcessPhoto(object image) {
            TakePhotoCommand.Execute(image);
        }

        public static readonly BindableProperty CameraErrorCommandProperty =
            BindableProperty.Create(propertyName: nameof(CameraErrorCommand),
                                    returnType: typeof(ICommand),
                                    declaringType: typeof(LabelReader));

        public void CameraError(string message) {
            CameraErrorCommand.Execute(message);
        }

        public void Cancel() {

        }

        /// <summary>
        /// The command for processing photo data.
        /// </summary>
        public ICommand TakePhotoCommand {
            get => (ICommand)GetValue(TakePhotoCommandProperty);
            set => SetValue(TakePhotoCommandProperty, value);
        }

        /// <summary>
        /// The command for processing a camera error.
        /// </summary>
        public ICommand CameraErrorCommand {
            get => (ICommand)GetValue(CameraErrorCommandProperty);
            set => SetValue(CameraErrorCommandProperty, value);
        }
    }
}