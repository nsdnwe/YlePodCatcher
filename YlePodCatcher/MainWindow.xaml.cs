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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Net;
using System.Xml.Serialization;
using System.Threading;
using FontAwesome.WPF;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Diagnostics;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System.Reflection;
using System.Security.Cryptography;

// Chrome Driver update:
// ---------------------
// Check Chrome version from Help, About Google Chrome
// Get correct driver from https://chromedriver.chromium.org/downloads
// Extract and copy to \bin\Debug

// https://www.toolsqa.com/selenium-webdriver/c-sharp/webelement-commands-in-c/


// \\192.168.100.110\YleDokumentit\
// Avaa https://areena.yle.fi/radio/a-o rullaa loppuun ja tallenna HTML koko sivusto nimellä saved-web-page.html => www.nsd.fi

// Install-Package FontAwesome.WPF

namespace YlePodCatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private const string appCaption = "Pod";
        private IList<Library> libraries; // Selected libraries
        private IList<Library> foundLibraries; // Found by Selenium
        private const string baseUrl = @"https://areena.yle.fi/radio/a-o/ladattavat/";

        private List<string> removedLibraries = new List<string>();

        /// <summary>
        /// Start application
        /// </summary>
        public MainWindow() {
            InitializeComponent();
            
            // Try to load old values

            Configuration conf = deserializeFromXml();
            if (conf.BaseFolder == null) {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                baseFolder.Text = path + "\\Yle Radiodokumentteja";
            } else {
                baseFolder.Text = conf.BaseFolder;
            }
            loadFoundLibrariesFromFile();
            checkWhichLibrariesHaveFolder(baseFolder.Text);
            createCheckboxListFromFoundLibraries(conf);
            clearSelection.IsEnabled = true;
            process.IsEnabled = true;
            instruction.Visibility = System.Windows.Visibility.Visible;
            //showOnlyRecommended.IsEnabled = true;
            //showAll.IsEnabled = true;
            process.Focus();


            Console.Out.WriteLine("Älä sulje tätä ikkunaa.");
            
        }

        /// <summary>
        /// Update button clicked
        /// </summary>
        private void onUpdateListClick(object sender, RoutedEventArgs e) {
            // Get libraries from yle web pages
            fillLibraryCheckboxListSelenium();
        }

        private void fillLibraryCheckboxListSelenium() {
            var driver = new ChromeDriver();
            foundLibraries = new List<Library>();
            loadRemovedLibrariesFromFile(); // => removedLibraries list

            libraryCheckboxList.Items.Clear();
            Configuration conf = deserializeFromXml();


            processTopLevel(driver, "Tiede", "https://areena.yle.fi/podcastit/ohjelmat/57-RyyrXny8e");
            processTopLevel(driver, "Historia", "https://areena.yle.fi/podcastit/ohjelmat/57-omYoDk4kY");
            processSecondLevel(driver, "Terveys ja hyvinvointi", "https://areena.yle.fi/podcastit/ohjelmat/30-1582");

            
            driver.Close();
            savefoundLibrariesToFile();
            createCheckboxListFromFoundLibraries(conf);
            Console.WriteLine("\r\nKansiot päivitetty. Valitse rastit.");
        }

        private void processTopLevel(ChromeDriver driver, string name, string url) {
            driver.Url = url;

            Console.WriteLine("\r\nRullaa sivun loppuun ja paina Enter\r\n");
            Console.ReadKey();

            // Get top level folders
            List<string> folderUrls = new List<string>();
            var topLevelList = driver.FindElements(By.LinkText("Näytä kaikki"));
            foreach (var topLevelFolder in topLevelList) {
                var parent = topLevelFolder.FindElement(By.XPath("./.."));
                string linkText = parent.GetAttribute("innerHTML");
                string href = getFirstHrefFromText(parent.GetAttribute("innerHTML"));
                folderUrls.Add("https://areena.yle.fi" + href);

                //Console.WriteLine(linkText + ": " + href);
                //Console.WriteLine();
            }

            // Process 2nd level folders
            foreach (var folderUrl in folderUrls) processSecondLevel(driver, name, folderUrl);
        }

        private void processSecondLevel(ChromeDriver driver, string name, string folderUrl) {

            driver.Url = folderUrl;

            Console.WriteLine("\r\nRullaa sivun loppuun ja paina Enter\r\n");
            Console.ReadKey();

            var mainContent = driver.FindElement(By.Id("maincontent"));
            string bigFatHtml = mainContent.GetAttribute("innerHTML");

            string head = "";
            string tail = "";
            string libraryID = "";
            string rawTitle = "";
            string rawHead = "";
            string title = "";
            string title2 = "";
            string desc = "";
            string thisCard;

            // Loop all SeriesCard__TitleLink.. elements

            while (true) {
                splitWell(bigFatHtml, "SeriesCard__TitleLink", true, out head, out tail);
                if (tail == "") break;

                splitWell(tail, "</article>", true, out thisCard, out bigFatHtml);
                string href = getFirstHrefFromText(thisCard);

                // Get lib id
                splitWell(href, "/1-", true, out head, out libraryID, true);

                splitWell(thisCard, "<span>", true, out head, out tail);
                splitWell(tail, "<span>", true, out head, out tail);
                splitWell(tail, "</span>", true, out title, out tail);

                // Has two lines?
                if(tail.StartsWith("<br>")) {
                    splitWell(tail, "<br><span>", true, out head, out tail);
                    splitWell(tail, "</span>", true, out title2, out tail);
                    title += " " + title2;
                    title = title.Replace("<span>","").Trim();
                }

                // Add result on page

                // Add line on check box list
                if (title != "null" && desc != "-1" && !removedLibraries.Any(z => z == libraryID)) {
                    Console.Out.WriteLine(title);
                    if (title.StartsWith("Länsimaisen sivistyksen")) {
                        string xx = "";
                    }

                    if (!foundLibraries.Any(z => z.LibraryID == libraryID)) foundLibraries.Add(new Library() { LibraryID = libraryID, Title = title });
                }
            }
        }

        private void createCheckboxListFromFoundLibraries(Configuration conf) {
            bool backcolored = false;
            Color backcoloredColor = Color.FromArgb(0xFF, 0xE8, 0xF8, 0xFF);

            foreach (var lib in foundLibraries.OrderBy(z => z.Title).ToList()) {
                string libraryID = lib.LibraryID;
                string title = lib.Title;
                string folderExist = "";
                if (lib.HasFolder) folderExist = "(f)";

                Button btnInfo = new Button();
                btnInfo.Content = " Info ";
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
                } else {
                    backcolored = true;
                }
                CheckBox cbo = new CheckBox();
                cbo.Name = "f" + libraryID;
                cbo.Width = 465;
                cbo.Margin = new Thickness(5);
                if (isOnList(conf, libraryID)) cbo.IsChecked = true;
                cbo.Content = title;
                if (lib.HasFolder) cbo.FontWeight = FontWeights.Bold;

                Label lab = new Label();
                lab.Content = "";
                lab.Margin = new Thickness(0);
                lab.Padding = new Thickness(0);

                //Label folEx = new Label();
                //folEx.Content = folderExist;
                //folEx.Margin = new Thickness(0);
                //folEx.Padding = new Thickness(0);
                //folEx.Width = 20;

                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                sp.Children.Add(btnInfo);
                sp.Children.Add(cbo);
                sp.Children.Add(lab);
                //sp.Children.Add(folEx);
                sp.Children.Add(btnDelete);
                lbi.Content = sp;
                libraryCheckboxList.Items.Add(lbi);
            }
        }

        private string getFirstHrefFromText(string text) {
            string head, tail;
            splitWell(text, "href=\"", true, out head, out tail);
            splitWell(tail, "\"", true, out head, out tail);
            return head;
        }


        private void updateListContent() {
            if (!checkBaseUrlAndFolder()) return;

            this.Cursor = Cursors.Wait;
            fillLibraryCheckboxListSelenium();
            this.Cursor = Cursors.Arrow;

            clearSelection.IsEnabled = true;
            process.IsEnabled = true;
            instruction.Visibility = System.Windows.Visibility.Visible;
            //showOnlyRecommended.IsEnabled = true;
            //showAll.IsEnabled = true;
            process.Focus();
        }

        /// <summary>
        /// Get files button clicked
        /// </summary>
        private void onProcessClick(object sender, RoutedEventArgs e) {
            if (baseFolder.Text.Trim() == "") {
                MessageBox.Show("Hakemisto puuttuu.", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get checkbox selections into a object
            makeLibraryListObject();

            // Save checkbox selection for next time
            serializeToXml();

            if (libraries.Count == 0) {
                MessageBox.Show("Yhtään ohjelmasarjaa ei ole valittuna.", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.Hide();

            Program.Libraries = libraries;
            Program.BaseFolderPath = baseFolder.Text;
            Program.BaseUrl = baseUrl;
            Program.GetFiles();
            //DownloadStatus ds = new DownloadStatus();
            //ds.Libraries = libraries;
            //ds.BaseFolderPath = baseFolder.Text;
            //ds.BaseUrl = baseUrl;
            //ds.ShowDialog();
            Application.Current.Shutdown();
        }

        private void onCloseClick(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void onClearSelection(object sender, RoutedEventArgs e) {
            if (MessageBox.Show("Poista kaikki checkbox-valinnat?", appCaption, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) return;
            for (int i = 0; i < libraryCheckboxList.Items.Count; i++) {
                ListBoxItem lbi = (ListBoxItem)libraryCheckboxList.Items[i];
                StackPanel sp = (StackPanel)lbi.Content;
                CheckBox cbo = (CheckBox)sp.Children[1];
                if (cbo.IsChecked == true) cbo.IsChecked = false;
            }
        }
        private void showOnlyRecommended_Click(object sender, RoutedEventArgs e) {
            this.Cursor = Cursors.Wait; // Yes, must be like this. Yay wpf!
            if (MessageBox.Show("Näytetäänkö listalla vain suositellut ohjelmasarjat?", appCaption, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) {
                this.Cursor = Cursors.Arrow;
                return;
            }

            try {
                using (WebClient client = new WebClient()) {
                    string text = client.DownloadString("https://nsd.fi/yle-pod-catcher/removed-libraries.txt");
                    string[] lines = text.Split('\n');
                    foreach (string line in lines) removeLibrary(line.Trim());
                }
            } catch (Exception ex) {
                this.Cursor = Cursors.Arrow;
                MessageBox.Show("Virhe: Suosituslistaa ei löydy. Tarkasta verkkoyhteyden tila ja kokeile hetken kuluttua uudelleen.", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            updateListContent();
            this.Cursor = Cursors.Arrow;

        }
        private void showAll_Click(object sender, RoutedEventArgs e) {
            this.Cursor = Cursors.Wait;
            if (MessageBox.Show("Näytetäänkö listalla kaikki ohjelmasarjat?", appCaption, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) {
                this.Cursor = Cursors.Arrow;
                return;
            }
            try {
                File.Delete("removed-libraries.txt");
                updateListContent();
                this.Cursor = Cursors.Arrow;
            } catch {
            }
        }

        /// <summary>
        /// Load available libraries and make a checkbox list
        /// </summary>
        private void fillLibraryCheckboxList()
        {
            // VANHA. 
            
            loadRemovedLibrariesFromFile(); // => removedLibraries list

            // Try to load old values

            Configuration conf = deserializeFromXml();

            libraryCheckboxList.Items.Clear();
            string url = baseUrl;
            if (!url.EndsWith("/")) url = url + "/";

            //string value = readHtml(url);
            Console.Out.WriteLine("Luetaan YlePage.html");

            string value = System.IO.File.ReadAllText(@"YlePage.html");
            int counter = 0;

            //value = System.IO.File.ReadAllText(@"saved-web-page.html");
            //try {
                //using (WebClient client = new WebClient()) {

                    //var htmlData = client.DownloadData("https://nsd.fi/yle-pod-catcher/saved-web-page.html");
                    //value = Encoding.UTF8.GetString(htmlData);
                //}
            //} catch (Exception ex) {
            //    MessageBox.Show("Virhe: Kirjastolistaa ei löydy. Tarkasta verkkoyhteyden tila ja kokeile hetken kuluttua uudelleen.", appCaption, MessageBoxButton.OK, MessageBoxImage.Error);
            //    this.Close();
            //    return;
            //}


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
            string rawTitle = "";
            string rawHead = "";
            string title = "";
            string desc = "";

            //splitWell(value, "card-list list", true, out head, out tail);
            string rest = value.Trim() + "END";

            while (true)
            {
                splitWell(rest, "href=\"/audio/1-", true, out head, out tail);
                if (tail == "") break;
                splitWell(tail, "\">", true, out libraryID, out tail);
                splitWell(tail, "</a>", true, out rawTitle, out tail);
                splitWell(rawTitle, ">", true, out rawHead, out title);
                splitWell(title, "</div>", true, out title, out rawHead);

                // <div class="CompactListCard__Title-ols5u1-1 bzFdFy">12 diktaattoria</div>

                rest = tail;

                desc = "";
                //if (getDescriptions.IsChecked.Value) desc = getDescription(libraryID);

                // Add line on check box list
                if (title != "null" && desc != "-1" && !removedLibraries.Any(z => z == libraryID))
                {
                    Console.Out.WriteLine(title);
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
                    counter++;
                }
            }
            Debug.WriteLine("Count: " + counter.ToString());
        }

        private void infoButton_Click(object sender, RoutedEventArgs e) {
            Button btn = (Button)sender;
            if (btn == null) return;
            string name = btn.Name;
            name = name.Substring(1, name.Length - 1);
            System.Diagnostics.Process.Start("https://areena.yle.fi/audio/1-" + name);
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
            string url = "https://areena.yle.fi/audio/1-" + libraryId;
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

        private void testing_Click(object sender, RoutedEventArgs e) {
            this.Hide();
            Program.GetFiles();
            Application.Current.Shutdown();
        }


        // ----------------------------------

        private static string getTextOrEmpty(ChromeDriver driver, string xPath) {
            try {
                return driver.FindElement(By.XPath(xPath)).Text;
            } catch {
                return "";
            }
        }
        private static string getTextOrEmpty(IWebElement webElement, string xPath) {
            try {
                return webElement.FindElement(By.XPath(xPath)).Text;
            } catch {
                return "";
            }
        }
        private static string getAttributeOrEmpty(ChromeDriver driver, string xPath, string attibute) {
            try {
                return driver.FindElement(By.XPath(xPath)).GetAttribute(attibute);
            } catch {
                return "";
            }
        }
        private static bool isLinkPresent(ChromeDriver driver, string text) {
            try {
                var element = driver.FindElement(By.PartialLinkText(text));
                return true;
            } catch {
                return false;
            }
        }

        private static bool isClassPresent(ChromeDriver driver, string className) {
            try {
                var cls = driver.FindElement(By.ClassName(className));
                return true;
            } catch {
                return false;
            }
        }

        private static bool isClassPresentInElement(IWebElement webElement, string className) {
            try {
                var cls = webElement.FindElement(By.ClassName(className));
                return true;
            } catch {
                return false;
            }
        }

        private static void waitUntilXpathElementAppears(ChromeDriver driver, string xPath, int timeout = 10) {
            int retryCount = 0;
            while (true) {
                try {
                    var cls = driver.FindElement(By.XPath(xPath));
                    return;
                } catch {
                    Thread.Sleep(1000);
                    retryCount++;
                    if (retryCount == timeout) throw new Exception("Element not found");
                }
            }
        }

        private static void waitUntilClassElementAppears(ChromeDriver driver, string className, int timeout = 10) {
            int retryCount = 0;
            while (true) {
                try {
                    var cls = driver.FindElement(By.ClassName(className));
                    return;
                } catch {
                    Thread.Sleep(1000);
                    retryCount++;
                    if (retryCount == timeout) throw new Exception("Element not found");
                }
            }
        }


        private static void splitWell(string fullString, string splitter, bool removeSplitter, out string head, out string tail, bool reverse = false) {
            head = fullString;
            tail = "";
            int pos = 0;
            if (reverse)
                pos = fullString.LastIndexOf(splitter);
            else
                pos = fullString.IndexOf(splitter);

            if (pos == -1) return;

            head = fullString.Substring(0, pos);

            var sl = splitter.Length;
            if (removeSplitter) {
                tail = fullString.Substring(pos + sl, fullString.Length - pos - sl);
            } else {
                tail = fullString.Substring(pos, fullString.Length - pos);
            }
        }

        private void savefoundLibrariesToFile() {
            StreamWriter sw = new StreamWriter("found-libraries.txt");
            foreach (var lib in foundLibraries) {
                sw.WriteLine(string.Format("\"{0}\";\"{1}\"", lib.LibraryID, lib.Title));
            }
            sw.Close();
        }

        private void loadFoundLibrariesFromFile() {
            foundLibraries = new List<Library>();
            try {
                var lines = File.ReadAllLines("found-libraries.txt");
                foreach (var line in lines) {
                    var parts = line.Split(';');
                    var lib = new Library();
                    lib.LibraryID = parts[0].Replace("\"", "");
                    lib.Title = parts[1].Replace("\"", "");
                    foundLibraries.Add(lib);
                }
            } catch {
            }
        }
        private void checkWhichLibrariesHaveFolder(string rootFolderName) {
            var folders = getFolders(rootFolderName);
            foreach (var folder in folders) {
                var lib = foundLibraries.FirstOrDefault(z => z.Title == folder);
                if (lib != null) lib.HasFolder = true;
            }
        }
        public List<string> getFolders(string folderName) {
            var folders = new List<string>();
            var files = Directory.GetDirectories(folderName);
            foreach (var file in files) {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)) {
                    folders.Add(fileInfo.Name);
                }
            }
            return folders;
        }
    }
}
