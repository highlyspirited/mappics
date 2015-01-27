using ExifUtils.Exif;
using ExifUtils.Exif.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mappics.ProcessWatcher;

namespace Mappics
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static ImageSource imgSourceOfNotAvailable;
        private static System.Windows.Shapes.Rectangle staticOverlayRectangle = null;
        private static Label staticMsgLabel = null;
        private static Image staticMapImage = null;

        public MainWindow()
        {
            InitializeComponent();

            imgSourceOfNotAvailable = mapImage.Source;
            staticMapImage = mapImage;
            staticMsgLabel = msgLabel;
            staticOverlayRectangle = overlayRectangle;

            startTimer();
        }

        private void updateImage(bool forceUpdate)
        {
            if (!Dispatcher.CheckAccess()) // CheckAccess returns true if you're on the dispatcher thread
            {
                Dispatcher.Invoke(() => updateImage(forceUpdate));
                return;
            }
            if (!IrfanWatcher.updateNeeded() && !forceUpdate)
            {
                return;
            }
            // choose an image
            //Console.Write("Enter image load path: ");
            string imagePath = IrfanWatcher.getFilePathToCurrentlyOpenedFileInIrfan();
            
            if (String.IsNullOrEmpty(imagePath))
            {
                return;
            }
            //Console.WriteLine();

            //----------------------------------------------

            // minimally loads image and closes it            
            ExifPropertyCollection properties = ExifReader.GetExifData(imagePath);

            ExifProperty gpsLatitudeRefProperty = properties.FirstOrDefault(p => p.DisplayName == "GPS Latitude Ref");
            if (gpsLatitudeRefProperty == null)
            {
                setImageReplacement("Picture does not contain GPS information");
                return;
            }
            else
            {
                removeImageReplacement();
            }
            string gpsLatitudeRef = "";
            if (gpsLatitudeRefProperty != null)
            {
                gpsLatitudeRef = gpsLatitudeRefProperty.DisplayValue;
            }
            ExifProperty gpsLatitudeProperty = properties.First(p => p.DisplayName == "GPS Latitude");
            double latitudeAsDouble = 0;
            if (gpsLatitudeProperty != null)
            {
                ExifUtils.Rational<uint>[] latitudeAsRational = (ExifUtils.Rational<uint>[])gpsLatitudeProperty.Value;
                double latitudeDegree = latitudeAsRational[0].Numerator / latitudeAsRational[0].Denominator;
                double latitudeMinute = latitudeAsRational[1].Numerator / latitudeAsRational[1].Denominator;
                double latitudeSecond = latitudeAsRational[2].Numerator / latitudeAsRational[2].Denominator;
                latitudeAsDouble = latitudeDegree + latitudeMinute / 60 + latitudeSecond / 3600;
            }
            if (gpsLatitudeRef == "S")
            {
                latitudeAsDouble *= -1;
            }

            ExifProperty gpsLongitudeRefProperty = properties.First(p => p.DisplayName == "GPS Longitude Ref");
            string gpsLongitudeRef = "";
            if (gpsLongitudeRefProperty != null)
            {
                gpsLongitudeRef = gpsLongitudeRefProperty.DisplayValue;
            }
            ExifProperty gpsLongitudeProperty = properties.First(p => p.DisplayName == "GPS Longitude");
            double longitudeAsDouble = 0;
            if (gpsLongitudeProperty != null)
            {
                ExifUtils.Rational<uint>[] longitudeAsRational = (ExifUtils.Rational<uint>[])gpsLongitudeProperty.Value;
                double longitudeDegree = longitudeAsRational[0].Numerator / longitudeAsRational[0].Denominator;
                double longitudeMinute = longitudeAsRational[1].Numerator / longitudeAsRational[1].Denominator;
                double longitudeSecond = longitudeAsRational[2].Numerator / longitudeAsRational[2].Denominator;
                longitudeAsDouble = longitudeDegree + longitudeMinute / 60 + longitudeSecond / 3600;
            }
            if (gpsLongitudeRef == "W")
            {
                longitudeAsDouble *= -1;
            }

            int picSizeHeight = (int)this.Height;
            int picSizeWidth = (int)this.Width;
            string apiKey = "";
            string mapType = "roadmap";
            string floatFormatString = "F6";
            string center = latitudeAsDouble.ToString(floatFormatString, CultureInfo.InvariantCulture.NumberFormat) + "," + longitudeAsDouble.ToString(floatFormatString, CultureInfo.InvariantCulture.NumberFormat); //"40.702147,-74.015794";
            string zoom = zoomLevel.ToString();
            string markerLocation = center;
            string size = picSizeWidth + "x" + picSizeHeight;
            System.Drawing.Image mapFromGMaps = null;
            if (String.IsNullOrEmpty(apiKey))
            {
                mapFromGMaps = GetImageFromUrl("http://maps.googleapis.com/maps/api/staticmap?center=" + center + "&maptype=" + mapType + "&zoom=" + zoom + "&markers=color:red|" + markerLocation + "&size=" + size); // + "&key=" + apiKey);
            }
            else
            {
                mapFromGMaps = GetImageFromUrl("http://maps.googleapis.com/maps/api/staticmap?center=" + center + "&maptype=" + mapType + "&zoom=" + zoom + "&markers=color:red|" + markerLocation + "&size=" + size + "&key=" + apiKey);
            }

            // ImageSource ...
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            MemoryStream ms = new MemoryStream();
            // Save to a memory stream...
            mapFromGMaps.Save(ms, ImageFormat.Png);
            // Rewind the stream...
            ms.Seek(0, SeekOrigin.Begin);
            // Tell the WPF image to use this stream...
            bi.StreamSource = ms;
            bi.EndInit();
            mapImage.Source = bi;
        }

        public static System.Drawing.Image GetImageFromUrl(string url)
        {
            using (var webClient = new WebClient())
            {
                return ByteArrayToImage(webClient.DownloadData(url));
            }
        }

        public static System.Drawing.Image ByteArrayToImage(byte[] fileBytes)
        {
            using (var stream = new MemoryStream(fileBytes))
            {
                return System.Drawing.Image.FromStream(stream);
            }
        }

        public static void setImageReplacement(string message)
        {
            if (staticOverlayRectangle != null)
            {
                staticOverlayRectangle.Opacity = 0.85;
            }
            if (staticMsgLabel != null)
            {
                staticMsgLabel.Content = message;
            }
            if (staticMapImage != null)
            {
                staticMapImage.Source = imgSourceOfNotAvailable;
            }
        }

        private void removeImageReplacement()
        {
            overlayRectangle.Opacity = 0.0;
            msgLabel.Content = "";
        }

        

        private void Window_Activated(object sender, EventArgs e)
        {
            //this.Width = System.Windows.SystemParameters.PrimaryScreenWidth;
            //this.Height = System.Windows.SystemParameters.PrimaryScreenHeight;
            this.Topmost = true;
            //this.Top = 0;
            //this.Left = 0;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Topmost = true;
            //this.Activate();
        }

        private void window_Loaded(object sender, RoutedEventArgs e)
        {
            updateImage(true);
        }
                 

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            window.Close();
        }

        private void Rectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        Timer _timer;
        private void startTimer()
        {
            _timer = new Timer(200); // Timer for 200 ms
            _timer.Elapsed += _timer_Elapsed;
            _timer.Enabled = true;
        }

        void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();
            updateImage(false);
            _timer.Start();
        }

        private void window_MouseEnter(object sender, MouseEventArgs e)
        {
            toolGrid.Opacity = 1;
            zoomGrid.Opacity = 1;
        }

        private void window_MouseLeave(object sender, MouseEventArgs e)
        {
            toolGrid.Opacity = 0.15;
            zoomGrid.Opacity = 0.01;
        }

        double resizeStep = 50;
        private void ResizeMinus_MouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            window.Height -= resizeStep;
            window.Width -= resizeStep;
            updateImage(true);
        }

        private void ResizePlus_MouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            window.Height += resizeStep;
            window.Width += resizeStep;
            updateImage(true);
        }

        int zoomLevel = 13;
        private void ZoomOut_MouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            --zoomLevel;
            updateImage(true);
        }

        private void ZoomIn_MouseLeftButtonDown(object sender, MouseEventArgs e)
        {
            ++zoomLevel;
            updateImage(true);
        }
    }


    
}
