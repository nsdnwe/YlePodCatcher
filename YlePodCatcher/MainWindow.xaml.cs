﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Net;
using System.Xml.Serialization;
using System.Threading;

// \\192.168.100.110\YleDokumentit\
// Avaa https://areena.yle.fi/radio/a-o rullaa loppuun ja tallenna HTML koko sivusto nimellä saved-web-page.html => www.nsd.fi

namespace YlePodCatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string appCaption = "Pod";
        private IList<Library> libraries;
        private string midUrl = "";
        private const string baseUrl = @"https://areena.yle.fi/radio/a-o/ladattavat/";

        private List<string> removedLibraries = new List<string>();
        private Random random = new Random();

        /// <summary>
        /// Start application
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Try to load old values

            Configuration conf = deserializeFromXml();
            if (conf.BaseFolder == null) {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                baseFolder.Text = path + "\\Yle Radiodokumentteja";
            } else {
                baseFolder.Text = conf.BaseFolder;
            }
        }

        /// <summary>
        /// Update button clicked
        /// </summary>
        private void onUpdateListClick(object sender, RoutedEventArgs e) {
            // Get libraries from yle web pages

            updateListContent();
        }

        private void updateListContent() {
            if (!checkBaseUrlAndFolder()) return;

            //infoText.Visibility = System.Windows.Visibility.Visible;
            this.Cursor = Cursors.Wait;

            fillLibraryCheckboxList();
            this.Cursor = Cursors.Arrow;

            clearSelection.IsEnabled = true;
            process.IsEnabled = true;
            instruction.Visibility = System.Windows.Visibility.Visible;
            showOnlyRecommended.IsEnabled = true;
            showAll.IsEnabled = true;
            process.Focus();
        }

        /// <summary>
        /// Get files button clicked
        /// </summary>
        private void onProcessClick(object sender, RoutedEventArgs e)
        {
            if (baseFolder.Text.Trim() == "")
            {
                MessageBox.Show("Hakemisto puuttuu.", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get checkbox selections into a object
            makeLibraryListObject();

            // Save checkbox selection for next time
            serializeToXml();

            if (libraries.Count == 0)
            {
                MessageBox.Show("Yhtään ohjelmasarjaa ei ole valittuna.", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.Hide();

            DownloadStatus ds = new DownloadStatus();
            ds.Libraries = libraries;
            ds.BaseFolderPath = baseFolder.Text;
            ds.BaseUrl = baseUrl;
            ds.MidUrl = midUrl;
            ds.ShowDialog();
            Application.Current.Shutdown();
        }

        private void onCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void onClearSelection(object sender, RoutedEventArgs e) {
            if (MessageBox.Show("Poista kaikki checkbox-valinnat?", appCaption, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) return;
            for (int i = 0; i < libraryCheckboxList.Items.Count; i++)
            {
                ListBoxItem lbi = (ListBoxItem)libraryCheckboxList.Items[i];
                StackPanel sp = (StackPanel)lbi.Content;
                CheckBox cbo = (CheckBox)sp.Children[0];
                if (cbo.IsChecked == true) cbo.IsChecked = false;
            }
        }
        private void showOnlyRecommended_Click(object sender, RoutedEventArgs e) {
            if (MessageBox.Show("Näytetäänkö listalla vain suositellut ohjelmasarjat?", appCaption, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) return;
            try {
                using (WebClient client = new WebClient()) {
                    string text = client.DownloadString("https://nsd.fi/yle-pod-catcher/removed-libraries.txt");
                    string[] lines = text.Split('\n');
                    foreach (string line in lines) removeLibrary(line.Trim());
                }
            } catch (Exception ex) {
                MessageBox.Show("Virhe: Suosituslistaa ei löydy. Tarkasta verkkoyhteyden tila ja kokeile hetken kuluttua uudelleen.", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            updateListContent();
        }
        private void showAll_Click(object sender, RoutedEventArgs e) {
            if (MessageBox.Show("Näytetäänkö listalla kaikki ohjelmasarjat?", "Vahvistus", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) return;
            try {
                File.Delete("removed-libraries.txt");
                updateListContent();
            } catch  {
            }
        }

        /// <summary>
        /// Load available libraries and make a checkbox list
        /// </summary>
        private void fillLibraryCheckboxList()
        {
            loadRemovedLibrariesFromFile(); // => removedLibraries list

            // Try to load old values

            Configuration conf = deserializeFromXml();

            libraryCheckboxList.Items.Clear();
            string url = baseUrl;
            if (!url.EndsWith("/")) url = url + "/";

            //string value = readHtml(url);
            string value = "";
                
            //value = System.IO.File.ReadAllText(@"saved-web-page.html");
            try {
                using (WebClient client = new WebClient()) {
                    var htmlData = client.DownloadData("https://nsd.fi/yle-pod-catcher/saved-web-page.html");
                    value = Encoding.UTF8.GetString(htmlData);
                }
            } catch (Exception ex) {
                MessageBox.Show("Virhe: Kirjastolistaa ei löydy. Tarkasta verkkoyhteyden tila ja kokeile hetken kuluttua uudelleen.", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }


            if (value == "")
            {
                MessageBox.Show("Tietoja ei löydy. Tarkasta ohjelmalistauksen web-osoite. Käytä muotoa: https://areena.yle.fi/radio/a-o", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            value = value.Replace("&Auml;", "Ä");
            value = value.Replace("&auml;", "ä");
            value = value.Replace("&Aring;", "Å");
            value = value.Replace("&aring;", "å");
            value = value.Replace("&Ouml;", "Ö");
            value = value.Replace("&ouml;", "ö");

            bool backcolored = false;
            Color backcoloredColor = Color.FromArgb(0xFF, 0xE8, 0xF8, 0xFF);

            string head = "";
            string tail = "";
            string libraryID = "";
            string title = "";
            string desc = "";

            splitWell(value, "card-list list", true, out head, out tail);
            string rest = tail.Trim() + "END";

            while (true)
            {
                splitWell(rest, "href=\"https://areena.yle.fi/1-", true, out head, out tail);
                if (tail == "") break;
                splitWell(tail, "\">", true, out libraryID, out tail);
                splitWell(tail, "</a>", true, out title, out tail);

                rest = tail;

                desc = "";
                //if (getDescriptions.IsChecked.Value) desc = getDescription(libraryID);

                // Add line on check box list
                if (title != "null" && desc != "-1" && !removedLibraries.Any(z => z == libraryID))
                {
                    Button btnInfo = new Button();
                    btnInfo.Content = " Info ";
                    //btnInfo.Content = "b" + libraryID;
                    btnInfo.Margin = new Thickness(0);
                    btnInfo.Name = "b" + libraryID;

                    btnInfo.AddHandler(Button.ClickEvent, new RoutedEventHandler(infoButton_Click)); // Add event hander for Info

                    Button btnDelete = new Button();
                    btnDelete.Content = " Poista listalta ";
                    btnDelete.Margin = new Thickness(0);
                    btnDelete.Name = "d" + libraryID;

                    btnDelete.AddHandler(Button.ClickEvent, new RoutedEventHandler(removeButton_Click)); // Add event hander for Delete

                    title = removeInvalidFileNameChars(title);
                    ListBoxItem lbi = new ListBoxItem();


                    lbi.Margin = new Thickness(1);
                    if (backcolored) {
                        lbi.Background = new SolidColorBrush(backcoloredColor);
                        backcolored = false;
                    }
                    else {
                        backcolored = true;
                    }
                    CheckBox cbo = new CheckBox();
                    cbo.Name = "f" + libraryID;
                    cbo.Width = 470;
                    cbo.Margin = new Thickness(5);
                    if (isOnList(conf, libraryID)) cbo.IsChecked = true;
                    cbo.Content = title;

                    Label lab = new Label();
                    lab.Content = desc;
                    lab.Margin = new Thickness(0);
                    lab.Padding = new Thickness(0);

                    StackPanel sp = new StackPanel();
                    sp.Orientation = Orientation.Horizontal;
                    sp.Children.Add(btnInfo);
                    sp.Children.Add(cbo);
                    sp.Children.Add(lab);
                    sp.Children.Add(btnDelete);
                    if (desc != "") {
                        Label tt = new Label();
                        tt.Content = desc;
                        sp.ToolTip = tt;
                    }

                    lbi.Content = sp;
                    libraryCheckboxList.Items.Add(lbi);
                }
            }
        }

        private void infoButton_Click(object sender, RoutedEventArgs e) {
            Button btn = (Button)sender;
            if (btn == null) return;
            string name = btn.Name;
            name = name.Substring(1, name.Length - 1);
            System.Diagnostics.Process.Start("https://areena.yle.fi/1-" + name);
        }

        private void removeButton_Click(object sender, RoutedEventArgs e) {
            Button btn = (Button)sender;
            if (btn == null) return;
            string name = btn.Name;
            name = name.Substring(1, name.Length - 1);
            removeLibrary(name);
            StackPanel sp = (StackPanel)btn.Parent;
            ListBoxItem lpi = (ListBoxItem)sp.Parent;
            libraryCheckboxList.Items.Remove(lpi);
        }

        private string getDescription(string libraryId) {
            string url = "https://areena.yle.fi/1-" + libraryId;
            string value = readHtml(url);

            string head = "";
            string tail = "";

            splitWell(value, "publication-info\">", true, out head, out tail);
            splitWell(tail, "</h2>", true, out head, out tail);

            // Return "-1" if podcast link not found 
            if (value.IndexOf("downloadable=true") == -1) return "-1";

            return head;
        }

        private void splitWell(string fullString, string splitter, bool removeSplitter, out string head, out string tail) {
            head = fullString;
            tail = "";
            int pos = fullString.IndexOf(splitter);
            if (pos == -1) return;

            head = fullString.Substring(0, pos);

            var sl = splitter.Length;
            if (removeSplitter) {
                tail = fullString.Substring(pos + sl, fullString.Length - pos - sl);
            }
            else {
                tail = fullString.Substring(pos, fullString.Length - pos);
            }
        }

        /// <summary>
        /// Check is this library already on selected
        /// </summary>
        private bool isOnList(Configuration conf, string libraryID)
        {
            if (conf.LibraryIDs == null) return false;
            foreach (string id in conf.LibraryIDs) if (id == libraryID) return true;
            return false;
        }

        /// <summary>
        /// Read HTML context of specific url
        /// </summary>
        private string readHtml(string url)
        {
            try
            {
                // Create a 'WebRequest' object with the specified url. 
                WebRequest myWebRequest = WebRequest.Create(url);

                // Send the 'WebRequest' and wait for response. 
                WebResponse myWebResponse = myWebRequest.GetResponse();

                // Obtain a 'Stream' object associated with the response object. 
                Stream ReceiveStream = myWebResponse.GetResponseStream();

                Encoding encode = System.Text.Encoding.GetEncoding("utf-8");

                // Pipe the stream to a higher level stream reader with the required encoding format. 
                StreamReader readStream = new StreamReader(ReceiveStream, encode);

                string strResponse = readStream.ReadToEnd();

                readStream.Close();

                // Release the resources of response object. 
                myWebResponse.Close();

                return strResponse;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Create library objects list
        /// </summary>
        private void makeLibraryListObject()
        {
            libraries = new List<Library>();

            for (int i = 0; i < libraryCheckboxList.Items.Count; i++)
            {
                ListBoxItem lbi = (ListBoxItem)libraryCheckboxList.Items[i];
                StackPanel sp = (StackPanel)lbi.Content;
                CheckBox cbo = (CheckBox)sp.Children[1];
                if (cbo.IsChecked == true)
                {
                    Library lib = new Library();
                    string name = cbo.Name;
                    lib.LibraryID = name.Remove(0, 1);      // Remove f
                    lib.Title = cbo.Content.ToString();
                    libraries.Add(lib);
                }
            }
        }

        /// <summary>
        /// Serialize form data into XML
        /// </summary>
        private void serializeToXml()
        {
            Configuration conf = new Configuration();
            conf.BaseFolder = baseFolder.Text;
            conf.BaseUrl = baseUrl;

            conf.LibraryIDs = new List<string>();

            // Save checkbox selections

            foreach (Library item in libraries) conf.LibraryIDs.Add(item.LibraryID);

            // Serialize in XML file

            XmlSerializer serializer = new XmlSerializer(typeof(Configuration));
            TextWriter textWriter = new StreamWriter(getApplicationPath() + "\\configuration.xml");
            serializer.Serialize(textWriter, conf);
            textWriter.Close();
         }

        /// <summary>
        /// Return configuration object from XML
        /// </summary>
        private Configuration deserializeFromXml()
        {
            Configuration conf;

            try
            {
                XmlSerializer mySerializer = new XmlSerializer(typeof(Configuration));
                FileStream myFileStream = new FileStream(getApplicationPath() + "\\configuration.xml", FileMode.Open);

                conf = (Configuration)mySerializer.Deserialize(myFileStream);
                myFileStream.Close();
            }
            catch (Exception)
            {
                conf = new Configuration();
            }
            return conf;
        }

        /// <summary>
        /// Remove chars which are not allowed in file name or in folder name
        /// </summary>
        private string removeInvalidFileNameChars(string input)
        {
            input = input.Replace(":", " -");
            input = input.Replace("?", "");
            input = input.Replace('/', '-');
            input = input.Replace('\\', '-');
            input = input.Replace('&', '-');
            input = input.Replace('%', '-');
            input = input.Replace('#', '-');
            input = input.Replace("\"", "");
            input = input.Replace('*', '-');
            input = input.Replace("!", "");
            input = input.Replace(";", "");
            if (input.EndsWith(".")) input.Remove(input.Length - 1, 1);
            return input.Trim();
        }

        /// <summary>
        /// Check is url and base folder valid
        /// </summary>
        private bool checkBaseUrlAndFolder()
        {
            string folder = baseFolder.Text;

            if (!folder.EndsWith("\\")) {
                baseFolder.Text += "\\";
                folder = baseFolder.Text;
            }

            if (folder == "" || !Directory.Exists(folder)) {
                try {
                    Directory.CreateDirectory(folder);
                } catch (Exception) {
                    MessageBox.Show(@"Tallennuskansiota ei voida luoda. Käytä muotoa: c:\Ylen-radiodokumentteja", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Return path to folder where application was started from
        /// </summary>
        private string getApplicationPath()
        {
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            return path.Replace(@"file:\", "");
        }

        private void browseFolder_Click(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result.ToString() != "OK") return;
            baseFolder.Text = dialog.SelectedPath;
        }

        private void removeLibrary(string name) {
            if (removedLibraries.Any(z => z == name)) return;
            StreamWriter sw = new StreamWriter("removed-libraries.txt", append:true);
            sw.WriteLine(name);
            sw.Close();
            removedLibraries.Add(name); // Needed?
        }

        private void loadRemovedLibrariesFromFile() {
            removedLibraries = new List<string>();
            try {
                var lines = File.ReadAllLines("removed-libraries.txt");
                foreach (var line in lines) removedLibraries.Add(line);
            } catch  {
            }
        }
    }
}
