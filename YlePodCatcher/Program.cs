using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace YlePodCatcher {
    static class Program {
        public static IList<Library> Libraries;
        public static string BaseFolderPath;
        public static string BaseUrl;

        private const string feedUrl = "http://feeds.yle.fi/areena/v1/series/1-{0}.rss?lang=fi&downloadable=true";
        private static int howManyNewFilesLoaded = 0;
        private static int howManyErros = 0;


        [STAThread]
        public static void Main(string[] args) {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        public static void GetFiles() {
            Console.WriteLine("Started");
            BaseUrl = BaseUrl.Replace("ohjelmat", "ohjelma");

            // Loop all the libraries
            foreach (var library in Libraries) {
                Console.WriteLine("");
                Console.WriteLine("-------------------------------------------------------------------------------");
                Console.WriteLine("Käsitellään ohjelmasarjaa: " + library.Title);
                Console.WriteLine("-------------------------------------------------------------------------------");
                Console.WriteLine("");

                // Handle one library
                makeSubfolderIfNeeded(library);

                // Get MP3 files of one library => liberary.Mp3Files
                getMp3ListOfOneLibrary(library);

                // Download files that are not already downloaded
                getMp3FilesOfOneLibrary(library);

            }

            Console.WriteLine("");
            Console.WriteLine(string.Format("Löytyi yhteensä {0} uutta tiedostoa.", howManyNewFilesLoaded));
            if (howManyErros > 0) Console.WriteLine(string.Format("Ladattaessa tiedostoja tapahtui {0} virhettä.", howManyErros));
            Console.WriteLine("Kaikki ohjelmasarja on käsitelty.");
            Console.Beep();

            Console.ReadKey();
        }

        /// <summary>
        /// Get all mp3 files in one library
        /// </summary>
        private static void getMp3FilesOfOneLibrary(Library library) {
            if (library.Mp3Files == null || library.Mp3Files.Count == 0) {
                Console.WriteLine("Yhtään Mp3 tiedostoa ei löytynyt.");
                return;
            }
            for (int i = library.Mp3Files.Count - 1; i >= 0; i--) {
                Mp3File mp3File = library.Mp3Files[i];
                string fileName = mp3File.Title;
                string fileID = mp3File.ID;
                double length = mp3File.Length;
                string folder = library.Title;

                if (!checkDoesMp3FileAlreadyExist(fileID, fileName, folder)) {
                    if (length > 0)
                        Console.WriteLine(string.Format("Ladataan tiedostoa: {0} ({1} MB)...", fileName, Math.Round(length, 2)));
                    else
                        Console.WriteLine(string.Format("Ladataan tiedostoa: {0}...", fileName));

                    // Actual download
                    downloadOneMp3File(library, mp3File);

                } else {
                    Console.WriteLine(string.Format("Tiedosto on jo ladattuna: {0}", fileName));
                }
            }
        }

        /// <summary>
        /// Download one Mp3 file
        /// </summary>
        private static void downloadOneMp3File(Library library, Mp3File mp3File) {
            string fullFolder = BaseFolderPath + library.Title;
            string fileName = mp3File.Title;
            string url = mp3File.Url;
            string id = mp3File.ID;
            string fileNumber = getNextNumber(fullFolder);

            string fullFileName = string.Format("{0}\\{1} {2} ({3}).mp3", fullFolder, fileNumber, fileName, id);

            using (WebClient client = new WebClient()) {
                try {
                    Debug.WriteLine("> Ladataan " + url + "; " + fullFileName);
                    client.DownloadFile(url, fullFileName);
                    howManyNewFilesLoaded++;
                } catch (Exception ex) {
                    howManyErros++;
                    Debug.WriteLine("Error: " + ex.ToString());
                    Console.WriteLine("Error: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Get list of mp3 files in one library
        /// </summary>
        private static void getMp3ListOfOneLibrary(Library library) {
            XmlTextReader rssReader;
            XmlDocument rssDoc;
            XmlNode nodeRss = null;
            XmlNode nodeChannel = null;
            XmlNode nodeItem;

            library.Mp3Files = new List<Mp3File>();

            string fullUrl = string.Format(feedUrl, library.LibraryID);
            rssReader = new XmlTextReader(fullUrl);
            rssDoc = new XmlDocument();
            try {
                rssDoc.Load(rssReader);
            } catch (Exception ex) {
                return;
            }

            // Loop for the <rss> tag
            for (int i = 0; i < rssDoc.ChildNodes.Count; i++) {
                // If it is the rss tag
                if (rssDoc.ChildNodes[i].Name == "rss") {
                    // <rss> tag found
                    nodeRss = rssDoc.ChildNodes[i];
                }
            }

            // Loop for the <channel> tag
            for (int i = 0; i < nodeRss.ChildNodes.Count; i++) {
                // If it is the channel tag
                if (nodeRss.ChildNodes[i].Name == "channel") {
                    // <channel> tag found
                    nodeChannel = nodeRss.ChildNodes[i];
                }
            }

            // Loop for the <title>, <link>, <description> and all the other tags
            for (int i = 0; i < nodeChannel.ChildNodes.Count; i++) {
                // If it is the item tag, then it has children tags which we will add as items to the ListView
                if (nodeChannel.ChildNodes[i].Name == "item") {
                    nodeItem = nodeChannel.ChildNodes[i];

                    // Get full path and filename etc.

                    XmlNode fileNameNode = nodeItem["enclosure"];
                    if (fileNameNode != null) {
                        string fileNameUrl = fileNameNode.Attributes["url"].Value.ToString();
                        string lengthString = "0";
                        if (fileNameNode.Attributes["length"] != null) lengthString = fileNameNode.Attributes["length"].Value.ToString();
                        long lengthLong = 0;
                        long.TryParse(lengthString, out lengthLong);
                        double length = ((double)lengthLong) / 1024 / 8;

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
                        string toFind = library.Title + " - ";
                        if (m.Title.StartsWith(toFind)) m.Title = m.Title.Remove(0, toFind.Length);
                        m.Url = longUrl;
                        m.Length = length;
                        m.ID = fileID;

                        library.Mp3Files.Add(m);
                    }
                }
            }

        }

        /// <summary>
        /// Make new subfolder if not exist
        /// </summary>
        private static void makeSubfolderIfNeeded(Library library) {
            string fullFolder = BaseFolderPath + library.Title + "\\";
            if (Directory.Exists(fullFolder)) return;
            Directory.CreateDirectory(fullFolder);
            Directory.CreateDirectory(fullFolder + @"\Kuunnellut");
        }

        /// <summary>
        /// Remove chars which are not allowed in file name or in folder name
        /// </summary>
        private static string removeInvalidFileNameChars(string input) {
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

        /// <summary>
        /// Check does this mp3 file exist
        /// </summary>
        private static bool checkDoesMp3FileAlreadyExist(string fileID, string fileName, string folder) {
            string fullFolder = BaseFolderPath + folder + "\\";
            return findFileID(fullFolder, fileID, fileName);
        }


        /// <summary>
        /// Find does fileid exist in any file name in this folder
        /// </summary>
        private static bool findFileID(string sourceDir, string fileID, string fileName) {

            // Process the list of files found in the directory. 

            string search = "(" + fileID + ")";
            string[] fileEntries = Directory.GetFiles(sourceDir);
            foreach (string fileNameInFolder in fileEntries) {
                // My Document Name (12345).mp3

                if (fileNameInFolder.IndexOf(search) > 0) return true;
                if (fileNameInFolder.IndexOf(fileName) > 0) return true;
            }

            // Recurse into subdirectories of this directory.

            string[] subdirEntries = Directory.GetDirectories(sourceDir);
            foreach (string subdir in subdirEntries)

                // Do not iterate through reparse points

                if ((File.GetAttributes(subdir) & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint) {
                    bool result = findFileID(subdir, fileID, fileName); // Recourse
                    if (result) return true;
                }
            return false;
        }

        /// <summary>
        /// Get next file number
        /// </summary>
        private static string getNextNumber(string fullFolder) {
            int number = 0;

            // Get last number
            try {
                XmlSerializer mySerializer = new XmlSerializer(typeof(int));
                FileStream myFileStream = new FileStream(fullFolder + "\\counter.xml", FileMode.Open);

                number = (int)mySerializer.Deserialize(myFileStream);
                myFileStream.Close();
            } catch (Exception) {
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

    }
}
