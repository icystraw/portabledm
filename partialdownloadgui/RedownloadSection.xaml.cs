using partialdownloadgui.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace partialdownloadgui
{
    /// <summary>
    /// Interaction logic for RedownloadSection.xaml
    /// </summary>
    public partial class RedownloadSection : Window
    {
        public RedownloadSection()
        {
            InitializeComponent();
        }

        private DownloadSection section;
        private Download download;

        public DownloadSection Section { get => section; set => section = value; }
        public Download Download { get => download; set => download = value; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (null == this.section) return;
            txtFileName.Text = section.FileName;
            txtFileSize.Text = section.Total.ToString() + " bytes (" + section.Start + '-' + section.End + ')';
            txtReStart.Text = section.Start.ToString();
            txtReEnd.Text = section.End.ToString();
        }

        private void DrawPortionView()
        {
            long start = GetStart();
            long end = GetEnd();
            if (start == (-1) || end == (-1) || end < start) return;
            // section.Start <= start <= end <= section.End
            decimal squares = 100;
            decimal bytesPerSquare = (decimal)section.Total / squares;
            decimal s1 = Math.Round((decimal)(start - section.Start + 1) / bytesPerSquare, MidpointRounding.AwayFromZero);
            decimal s2 = Math.Round((decimal)(end - start + 1) / bytesPerSquare, MidpointRounding.AwayFromZero);
            decimal s3 = Math.Round((decimal)(section.End - end + 1) / bytesPerSquare, MidpointRounding.AwayFromZero);
            wpPortionView.Children.Clear();
            for (int i = 0; i < s1; i++)
            {
                wpPortionView.Children.Add(GetRectangle(false));
            }
            for (int i = 0; i < s2; i++)
            {
                wpPortionView.Children.Add(GetRectangle(true));
            }
            for (int i = 0; i < s3; i++)
            {
                wpPortionView.Children.Add(GetRectangle(false));
            }
        }

        private Rectangle GetRectangle(bool isDownloading)
        {
            Rectangle r = new();
            r.Height = 7;
            r.Width = 3;
            r.RadiusX = 2;
            r.RadiusY = 2;
            r.Margin = new Thickness(1);
            if (isDownloading)
            {
                r.Fill = Brushes.OrangeRed;
            }
            else
            {
                r.Fill = Brushes.LightSeaGreen;
            }
            return r;
        }

        private long GetStart()
        {
            long start = (-1);
            try
            {
                start = Convert.ToInt64(txtReStart.Text.Trim());
                if (start < section.Start || start > section.End) return (-1);
            }
            catch { }
            return start;
        }

        private long GetEnd()
        {
            long end = (-1);
            try
            {
                end = Convert.ToInt64(txtReEnd.Text.Trim());
                if (end < section.Start || end > section.End) return (-1);
            }
            catch { }
            return end;
        }


        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            long start = GetStart();
            long end = GetEnd();
            if (start == (-1) || end == (-1) || end < start)
            {
                MessageBox.Show("Invalid download range.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!File.Exists(section.FileName) || (new FileInfo(section.FileName)).Length != section.Total)
            {
                MessageBox.Show("Original file does not exists or is not correct size.", "Message", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DownloadSection newDs = new();
            newDs.Url = section.Url;
            newDs.Start = start;
            newDs.End = end;
            newDs.UserName = section.UserName;
            newDs.Password = section.Password;
            newDs.ParentFile = section.FileName;
            try
            {
                Util.DownloadPreprocess(newDs);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (newDs.DownloadStatus == DownloadStatus.DownloadError)
            {
                MessageBox.Show(newDs.Error, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            download = new();
            download.SummarySection = newDs;
            download.Sections.Add(download.SummarySection.Clone());
            download.NoDownloader = cbThreads.SelectedIndex + 1;
            download.DownloadFolder = System.IO.Path.GetDirectoryName(newDs.ParentFile);
            this.DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void txtReStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            DrawPortionView();
        }
    }
}
