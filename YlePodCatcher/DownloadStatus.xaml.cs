using System;
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
using System.Windows.Shapes;
using System.ComponentModel;
using System.Xml;
using System.IO;
using System.Net;
using System.Xml.Serialization;
using System.Windows.Threading;
using System.Diagnostics;

namespace YlePodCatcher
{
    /// <summary>
    /// Interaction logic for DownloadStatus.xaml
    /// </summary>
    public partial class DownloadStatus : Window
    {
        public string BaseFolderPath;
        public string BaseUrl;
        public string MidUrl;
        public IList<Library> Libraries;

        private const string feedUrl = "http://feeds.yle.fi/areena/v1/series/1-{0}.rss?lang=fi&downloadable=true";

        private BackgroundWorker worker;
        private bool stop = false;

        private int libraryIndex = 0;
        private int mp3Index;
        private int mp3ReverseIndex;

        private string fileToDeleteIfDownloadIsCancelled = "";
        private string lastError = "";

        private int howManyNewFilesLoaded = 0;
        private int howManyErros = 0;
        private bool noMoreRefesh = false; // Set true when first mp3 file found

        /// <summary>
        /// Initialize async background worker
        /// </summary>
        public DownloadStatus()
        {
            InitializeComponent();

            worker = new BackgroundWorker();
            worker.DoWork += loadOneMp3;
            worker.RunWorkerCompleted += workerCompleted;
        }

        /// <summary>
        /// Start download process when form is rendered. START IS HERE
        /// </summary>
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            addStatusLine("");
            BaseUrl = BaseUrl.Replace("ohjelmat", "ohjelma");
            downloadNext();
        }

        /// <summary>
        /// Get list of mp3 files in one library
        /// </summary>
        private void getMp3ListOfOneLibrary()
        {
            XmlTextReader rssReader;
            XmlDocument rssDoc;
            XmlNode nodeRss = null;
            XmlNode nodeChannel = null;
            XmlNode nodeItem;

            Libraries[libraryIndex].Mp3Files = new List<Mp3File>();

            string fullUrl = string.Format(feedUrl, Libraries[libraryIndex].LibraryID);
            rssReader = new XmlTextReader(fullUrl);
            rssDoc = new XmlDocument();
            try 
	        {	        
                rssDoc.Load(rssReader);
	        }
	        catch (Exception ex)
	        {
                return;
            }

            // Loop for the <rss> tag
            for (int i = 0; i < rssDoc.ChildNodes.Count; i++)
            {
                // If it is the rss tag
                if (rssDoc.ChildNodes[i].Name == "rss")
                {
                    // <rss> tag found
                    nodeRss = rssDoc.ChildNodes[i];
                }
            }

            // Loop for the <channel> tag
            for (int i = 0; i < nodeRss.ChildNodes.Count; i++)
            {
                // If it is the channel tag
                if (nodeRss.ChildNodes[i].Name == "channel")
                {
                    // <channel> tag found
                    nodeChannel = nodeRss.ChildNodes[i];
                }
            }

            // Loop for the <title>, <link>, <description> and all the other tags
            for (int i = 0; i < nodeChannel.ChildNodes.Count; i++)
            {
                // If it is the item tag, then it has children tags which we will add as items to the ListView
                if (nodeChannel.ChildNodes[i].Name == "item")
                {
                    nodeItem = nodeChannel.ChildNodes[i];

                    // Get full path and filename etc.

                    XmlNode fileNameNode = nodeItem["enclosure"];
                    if (fileNameNode != null)
                    {
                        string fileNameUrl = fileNameNode.Attributes["url"].Value.ToString();
                        string lengthString = "0";
                        if (fileNameNode.Attributes["length"] != null) lengthString = fileNameNode.Attributes["length"].Value.ToString();
                        long lengthLong = 0;
                        long.TryParse(lengthString, out lengthLong);
                        double length = ((double)lengthLong) / 1024 / 1024;

                        string[] parts = fileNameUrl.Split('?');
                        string longUrl = parts[0];

                        string[] parts2 = longUrl.Split('/');
                        string fileIDBase = "-" + parts2[parts2.Length - 1];
                        string[] parts3 = fileIDBase.Split('-');
                        string fileID = parts3[parts3.Length - 1];

                        fileID = fileID.Replace(".mp3", "");

                        // Make new Mp3File object

                        Mp3File m = new Mp3File();
                        m.Title = removeInvalidFileNameChars(nodeItem["title"].InnerText);
                        string toFind = Libraries[libraryIndex].Title + " - ";
                        if (m.Title.StartsWith(toFind)) m.Title = m.Title.Remove(0, toFind.Length);
                        m.Url = longUrl;
                        m.Length = length;
                        m.ID = fileID;

                        Libraries[libraryIndex].Mp3Files.Add(m);
                    }
                }
            }
        }

        /// <summary>
        /// Make new subfolder if not exist
        /// </summary>
        private void makeSubfolderIfNeeded()
        {
            string fullFolder = BaseFolderPath + Libraries[libraryIndex].Title + "\\";
            if (Directory.Exists(fullFolder)) return;
            Directory.CreateDirectory(fullFolder);
            Directory.CreateDirectory(fullFolder + @"\Kuunnellut");
        }

        /// <summary>
        /// Check does this mp3 file exist
        /// </summary>
        private bool checkDoesMp3FileAlreadyExist(string fileID, string fileName)
        {
            string fullFolder = BaseFolderPath + Libraries[libraryIndex].Title + "\\";
            return findFileID(fullFolder, fileID, fileName);
        }

        /// <summary>
        /// Find does fileid exist in any file name in this folder
        /// </summary>
        public bool findFileID(string sourceDir, string fileID, string fileName)
        {

            // Process the list of files found in the directory. 

            string search = "(" + fileID + ")";
            string[] fileEntries = Directory.GetFiles(sourceDir);
            foreach (string fileNameInFolder in fileEntries)
            {
                // My Document Name (12345).mp3

                if (fileNameInFolder.IndexOf(search) > 0) return true;
                if (fileNameInFolder.IndexOf(fileName) > 0) return true;
            }

            // Recurse into subdirectories of this directory.

            string[] subdirEntries = Directory.GetDirectories(sourceDir);
            foreach (string subdir in subdirEntries)

                // Do not iterate through reparse points

                if ((File.GetAttributes(subdir) & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
                {
                    bool result = findFileID(subdir, fileID, fileName); // Recourse
                    if (result) return true;
                }
            return false;
        }

        /// <summary>
        /// Get next file number
        /// </summary>
        private string getNextNumber(string fullFolder)
        {
            int number = 0;

            // Get last number
            try
            {
                XmlSerializer mySerializer = new XmlSerializer(typeof(int));
                FileStream myFileStream = new FileStream(fullFolder + "\\counter.xml", FileMode.Open);

                number = (int)mySerializer.Deserialize(myFileStream);
                myFileStream.Close();
            }
            catch (Exception)
            {
                number = 0;
            }

            number++;

            // Save new number

            XmlSerializer serializer = new XmlSerializer(typeof(int));
            TextWriter textWriter = new StreamWriter(fullFolder + "\\counter.xml");
            serializer.Serialize(textWriter, number);
            textWriter.Close();

            string value = "000" + number.ToString();
            return value.Substring(value.Length - 3, 3);
        }

        /// <summary>
        /// Download next mp3 file. Select library and set async load values
        /// </summary>
        private void downloadNext()
        {
            // Loop files in one library

            if (Libraries[libraryIndex].Mp3Files == null)
            {
                // New library, mp3 files not yet listed

                this.Cursor = Cursors.Wait;
                addStatusLine("");
                addStatusLine("Käsitellään ohjelmasarjaa: " + Libraries[libraryIndex].Title);
                if (!noMoreRefesh) status.Refresh();
                noMoreRefesh = true;
                progressBar.Value = 0;
                makeSubfolderIfNeeded();
                getMp3ListOfOneLibrary();
                mp3Index = 0;
                mp3ReverseIndex = Libraries[libraryIndex].Mp3Files.Count - 1;
                this.Cursor = Cursors.Arrow;
            }
            else
            {
                if (mp3Index < Libraries[libraryIndex].Mp3Files.Count - 1)
                {
                    // Next mp3 file

                    mp3Index++;
                    mp3ReverseIndex = Libraries[libraryIndex].Mp3Files.Count - 1 - mp3Index;
                }
                else
                {
                    // All mp3 files loaded, moveto next library

                    libraryIndex++;
                    if (libraryIndex == Libraries.Count)
                    {
                        addStatusLine("");
                        addStatusLine(string.Format("Löytyi yhteensä {0} uutta tiedostoa.", howManyNewFilesLoaded));
                        if (howManyErros > 0) addStatusLine(string.Format("Ladattaessa tiedostoja tapahtui {0} virhettä.", howManyErros));
                        addStatusLine("Kaikki ohjelmasarja on käsitelty.");
                        progressBar.Value = 0;
                        pause.Visibility = System.Windows.Visibility.Hidden;
                        stop = true;
                        return;
                    }
                    downloadNext();     // Recursion
                    return;
                }
            }

            // Check is everything ready for loading one mp3 file in async mode 

            if (Libraries[libraryIndex].Mp3Files != null && Libraries[libraryIndex].Mp3Files.Count > 0)
            {
                string fileName = Libraries[libraryIndex].Mp3Files[mp3ReverseIndex].Title;
                string fileID = Libraries[libraryIndex].Mp3Files[mp3ReverseIndex].ID;
                double length = Libraries[libraryIndex].Mp3Files[mp3ReverseIndex].Length;

                progressBar.Value = 100 * ((double)(mp3Index + 1) / (double)(Libraries[libraryIndex].Mp3Files.Count + 1));

                if (!checkDoesMp3FileAlreadyExist(fileID, fileName))
                {
                    if (length > 0)
                        addStatusLine(string.Format("Ladataan tiedostoa: {0} ({1} MB)...", fileName, Math.Round(length, 2)));
                    else
                        addStatusLine(string.Format("Ladataan tiedostoa: {0}...", fileName));

                    worker.RunWorkerAsync(); // Load one mp3
                }
                else
                {
                    addStatusLine(string.Format("Tiedosto on jo ladattuna: {0}", fileName));
                    downloadNext();     // Recursion
                }
            }
            else
            {
                addStatusLine("Yhtään Mp3 tiedostoa ei löytynyt.");
                downloadNext();     // Recursion
            }
        }

        /// <summary>
        /// Async. Load one MP3
        /// </summary>
        private void loadOneMp3(object sender, DoWorkEventArgs e)
        {
            worker = sender as BackgroundWorker;
            string fullFolder = BaseFolderPath + Libraries[libraryIndex].Title;
            string fileName = Libraries[libraryIndex].Mp3Files[mp3ReverseIndex].Title;
            string url = Libraries[libraryIndex].Mp3Files[mp3ReverseIndex].Url;
            string id = Libraries[libraryIndex].Mp3Files[mp3ReverseIndex].ID;
            string fileNumber = getNextNumber(fullFolder);

            string fullFileName = string.Format("{0}\\{1} {2} ({3}).mp3", fullFolder, fileNumber, fileName, id);

            using (WebClient client = new WebClient())
            {
                try
                {
                    Debug.WriteLine("Downloading " + url + "; " + fullFileName);

                    client.DownloadFile(url, fullFileName);
                    howManyNewFilesLoaded++;
                    fileToDeleteIfDownloadIsCancelled = fullFileName;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error: " + ex.ToString());
                    lastError = ex.Message;
                }
            }
            
        }

        /// <summary>
        /// Download completed. Get next file
        /// </summary>
        private void workerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            fileToDeleteIfDownloadIsCancelled = "";
            if (lastError != "")
            {
                addStatusLine("VIRHE: " + lastError);
                lastError = "";
                howManyErros++;
            }
            if (!stop) downloadNext();
        }

        /// <summary>
        /// Close application
        /// </summary>
        private void onCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (fileToDeleteIfDownloadIsCancelled != "")
            {
                // First delete worker thread. How?
                // File.Delete(fileToDeleteIfDownloadIsCancelled);
            }
        }

        /// <summary>
        /// User clicks pause
        /// </summary>
        private void onPauseClick(object sender, RoutedEventArgs e)
        {
            worker.CancelAsync();
            stop = true;
            progressBar.Value = 0;
            pause.Visibility = System.Windows.Visibility.Hidden;
        }

        /// <summary>
        /// Add line in status textbox
        /// </summary>
        private void addStatusLine(string value)
        {
            status.Text += value + "\r\n";
            status.Focus();
            status.SelectionStart = status.Text.Length;
            status.SelectionLength = 0;
            Debug.WriteLine(value);
        }

        /// <summary>
        /// Remove chars which are not allowed in file name or in folder name
        /// </summary>
        private string removeInvalidFileNameChars(string input)
        {
            input = input.Replace(":", " -");
            input = input.Replace("?", "");
            input = input.Replace("/", " of ");
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
    }

    /// <summary>
    /// Extension method to add Refesh() method for UI controls
    /// </summary>
    public static class ExtensionMethods 
    { 
        private static Action EmptyDelegate = delegate() { }; 
        public static void Refresh(this UIElement uiElement) 
        { 
            uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate); 
        } 
    }
}
