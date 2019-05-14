using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinImageCaptureSample.Services;
using XamarinImageCaptureSample.ViewModels;
using XamarinImageCaptureSample.Views;

namespace XamarinImageCaptureSample
{
    public partial class App : Application
    {

        public App()
        {
            MainPage = new LabelReaderPage()
            {
                BindingContext = new LabelReaderPageViewModel()
            };
            InitializeComponent();
        }
    }
}
