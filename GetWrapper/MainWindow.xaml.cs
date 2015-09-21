using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Timers;
using Utilities.CommandLine;

namespace GetWrapper
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class MTObservableCollection<T> : SynchronizedObservableCollection<T>
        {
            public override event NotifyCollectionChangedEventHandler CollectionChanged;
            protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            {
                NotifyCollectionChangedEventHandler CollectionChanged = this.CollectionChanged;
                if (CollectionChanged != null)
                    foreach (NotifyCollectionChangedEventHandler nh in CollectionChanged.GetInvocationList())
                    {
                        DispatcherObject dispObj = nh.Target as DispatcherObject;
                        if (dispObj != null)
                        {
                            Dispatcher dispatcher = dispObj.Dispatcher;
                            if (dispatcher != null && !dispatcher.CheckAccess())
                            {
                                dispatcher.BeginInvoke(
                                    (Action)(() => nh.Invoke(this,
                                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset))),
                                    DispatcherPriority.DataBind);
                                continue;
                            }
                        }
                        nh.Invoke(this, e);
                    }
            }
        }

        private MTObservableCollection<LogLine> loglist;
        private MTObservableCollection<LogLine> errorlist;
        private bool commandWindowActive;
       
        private System.Timers.Timer tmrTimersTimer;
        private static object _lock = new object();

        public class LogList : MTObservableCollection<LogLine>
        {
            public LogList()
                : base()
            {

            }

        };

        public MainWindow()
        {
            loglist = new LogList();
            errorlist = new LogList();
            commandWindowActive = false;
            InitializeComponent();
            LoggingListview.ItemsSource = loglist;
            //ErrorListview.ItemsSource = errorlist;
            BindingOperations.EnableCollectionSynchronization(loglist, _lock);

            tmrTimersTimer = new System.Timers.Timer();
            tmrTimersTimer.Interval = 1000 * 60 * 60;                           // hourly
            tmrTimersTimer.Elapsed += new ElapsedEventHandler(tmrTimersTimer_Elapsed); 
            tmrTimersTimer.Start();

            getPrograms(retrieveProgramsFromTextListing());
            tmrTimersTimer.Start();

           
        }
     
        private void tmrTimersTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) 
        { 
            // Do something on the UI thread (same thread the form was 
            // created on)... 
            // If we didn't set SynchronizingObject we would be on a 
            // worker thread... 

            loglist.Add(new LogLine() 
            { 
                thisLine = "timer tripped"
            }
            );

            tmrTimersTimer.Stop();
            getPrograms(retrieveProgramsFromTextListing());
            tmrTimersTimer.Start();

        }


        public void logCallback(string inward)
        {

            lock (this)
            {

                lock (loglist)
                {
                    while (loglist.Count > 200)
                        loglist.RemoveAt(0);
                }

                lock (loglist)
                {
                    loglist.Add(new LogLine() { thisLine = inward });
                }

                using (StreamWriter w = File.AppendText(@"D:\Visual Studio 2015\Projects\GetWrapper\GetWrapper\log.txt"))
                {
                    w.WriteLine("{0} {1} {2}", DateTime.Now.ToLongTimeString(),
                        DateTime.Now.ToLongDateString(), inward);
                    //w.WriteLine("  :");
                    //w.WriteLine("  :{0}", inward);
                }
            }
        }

        private void CmdOutputDataReceived(object sender, DataReceivedEventArgs e)
        {

            logCallback(e.Data);
            commandWindowActive = false;

            //loglist.Add(new LogLine() { thisLine = e.Data });
            //using (StreamWriter w = File.AppendText(@"C:\Users\phil\Documents\Visual Studio 2012\Projects\GetWrapper\GetWrapper\log.txt"))
            //{
            //    w.WriteLine("{0} {1} {2}", 
            //        DateTime.Now.ToLongDateString(),
            //        DateTime.Now.ToLongTimeString(),
            //        e.Data);
            //    //w.WriteLine("  :");
            //    //w.WriteLine("  :{0}", inward);
            //}
        }

        private void CmdOutputError(object sender, DataReceivedEventArgs e)
        {
            errorlist.Add(new LogLine() { thisLine = e.Data });
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

            correctFailures();

            tmrTimersTimer.Stop();
            getPrograms(retrieveProgramsFromTextListing());
            tmrTimersTimer.Start();
        }

        private string drive_get_iplayer(string commandLineArguments)
        {

            CommandLineHelper CLH = new CommandLineHelper();

            Action<string> output = new Action<string>(logCallback);

            DataReceivedEventHandler logHandler = new DataReceivedEventHandler(CmdOutputDataReceived);
            DataReceivedEventHandler errorHandler = new DataReceivedEventHandler(CmdOutputError);

            if (commandWindowActive)
            {
                string logLine2 = "second launch attempted: %s " + commandLineArguments;
                logCallback(logLine2);
                return logLine2;

            }

            commandWindowActive = true;
            string xmlLogOutput = CLH.Run(@"c:\program files (x86)\get_iplayer\get_iplayer.cmd", commandLineArguments, logHandler,errorHandler);

            string logLine = "get_iplayer " + commandLineArguments;
            logCallback(logLine);
            //loglist.Add(new LogLine() { thisLine = logLine });

            return xmlLogOutput;
        }

        private List<string> retrieveProgramsFromTextListing()
        {

            //string file = @"C:\Users\phil\Desktop\BBC Downloads.txt";
            string file = @"D:\Visual Studio 2015\Projects\GetWrapper\GetWrapper\bbcPrograms.txt";

            List<string> programList = new List<string>();

            Encoding fileEncoding = GetFileEncoding(file);

            using (StreamReader r = new StreamReader(file, fileEncoding, true))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {

                    if ((line.Length != 0) &&
                       (line.IndexOf("rem") == -1))
                        programList.Add(line);
                }
            }

            return programList; //test

        }

        private string retrieveProgramsInformation(string pid)
        {
            string commandLineArguments
                = String.Format(@"--info ""{0}""",
                  "--pid");

            return drive_get_iplayer(commandLineArguments);


        }

        private void correctFailures()
        {

            // Book of the Week - A Long Walk Home Episode 3 b036knvq default part01.wma
            string[] filePaths = Directory.GetFiles(@"D:\Visual Studio 2015\incoming\", "*.wma");

            //string[] extensions = new[] { ".wma", ".flv" };


            //FileInfo[] files =
            //    Directory.GetFiles()
            //         .Where(f => extensions.Contains(f.Extension.ToLower()))
            //         .ToArray();

            //FileInfo[] files =
            //    Directory.EnumerateFiles()
            //         .Where(f => extensions.Contains(f.Extension.ToLower()))
            //         .ToArray();



            foreach (string badFile in filePaths)
            {

                // this retrieval has apparently failed: tyr and extract the pid

                int defaultLocation = badFile.IndexOf("default");

                if (defaultLocation == -1)
                    continue;

                int pidStart = defaultLocation - 9;

                if (pidStart < 0)
                    continue;

                string logString = "Re-requesting " + badFile + "...";
                loglist.Add(new LogLine() { thisLine = logString });

                string pid = badFile.Substring(pidStart,8);

                string renamedFile = badFile.Replace(".wma", ".old");

                try
                {
                    File.Move(badFile, renamedFile);
                }

                catch (Exception e) 
                { ; 
                } 

                retrieveProgramFromPid(pid);

            }

        }


        private void removeUnderscores()
        {

            // Book of the Week - A Long Walk Home Episode 3 b036knvq default part01.wma
            string[] filePaths = Directory.GetFiles(@"D:\incoming\", "*.m4a");

            foreach (string filename in filePaths)
            {

                if (filename.IndexOf("_") != -1)
                {
                    string renamedFile = filename.Replace("_", " ");

                    if (File.Exists(renamedFile))
                    {
                        File.Delete(filename);
                    }

                    else {

                        try
                        {
                            File.Move(filename, renamedFile);
                        }

                        catch (Exception e)
                        {
                            ;
                        } 
                    }

                    
                }
            }

        }

        private string getPrograms(List<string> programList)
        {

            loglist.Add(new LogLine() { thisLine = "retrieval beginning..." });

            string listing = string.Join("\" \"", programList.ToArray());

            string commandLineArguments
                = String.Format(@"--get ""{0}"" --type=radio",
                  listing);

            string returnString =  drive_get_iplayer(commandLineArguments);

            correctFailures();
            removeUnderscores();

            // loglist.Add(new LogLine() { thisLine = "retrieval complete." } );
            
            return returnString;

        }


        // get_iplayer --get "classic serial" --type="radio" --force --output "C:\Users\Public\Documents\common"

        private string retrieveProgramFromPid(string pid)
        {

            loglist.Add(new LogLine()
            {
                thisLine = "retrieving pid " + pid + "..."
            }
            );

            // get_iplayer --pid b002a23a

            string commandLineArguments
                = String.Format(@"--pid ""{0}"" --type=radio --force",
                  pid);

            return drive_get_iplayer(commandLineArguments);
        }

        private void storeRadioCache()
        {
            string returnedCache = refreshRadioCache();
            System.IO.File.WriteAllText(@"C:\Users\Public\RadioCache.txt", returnedCache);
        }

        private string refreshRadioCache()
        {
            string commandLineArguments
                = String.Format(@"--refresh --type=radio");

            return drive_get_iplayer(commandLineArguments);
        }

        private string refreshTVCache()
        {
            string commandLineArguments
                = String.Format(@"--refresh --type=tv");

            return drive_get_iplayer(commandLineArguments);
        }

        private string showDefaultUserSettings()
        {
            string commandLineArguments
                = String.Format(@"--prefs-show");

            return drive_get_iplayer(commandLineArguments);
        }

        private string addToDefaultPreferences(string commandLine)
        {
            string commandLineArguments
                = String.Format(@"--prefs-add ""{0}""",commandLine);

            return drive_get_iplayer(commandLineArguments);
        }

        private string removeFromDefaultPreferences(string commandLine)
        {
            string commandLineArguments
                = String.Format(@"--prefs-del ""{0}""", commandLine);

            return drive_get_iplayer(commandLineArguments);
        }

        private string turnOnMP3conversion()
        {
            return addToDefaultPreferences("--aactomp3");
        }

        private string turnOffMP3conversion()
        {
            return removeFromDefaultPreferences("--aactomp3");
        }

        /// <summary>

        /// Detects the byte order mark of a file and returns

        /// an appropriate encoding for the file.

        /// </summary>

        /// <param name="srcFile"></param>

        /// <returns></returns>

        public static Encoding GetFileEncoding(string srcFile)
        {

            // *** Use Default of Encoding.Default (Ansi CodePage)

            Encoding enc = Encoding.Default;



            // *** Detect byte order mark if any - otherwise assume default

            byte[] buffer = new byte[5];

            FileStream file = new FileStream(srcFile, FileMode.Open);

            file.Read(buffer, 0, 5);

            file.Close();

            if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
                enc = Encoding.UTF8;

            else if (buffer[0] == 0xff && buffer[1] == 0xfe)
                enc = Encoding.Unicode; // utf-16le

            else if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
                enc = Encoding.UTF32;

            else if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
                enc = Encoding.UTF7;



            return enc;

        }
    }

    public class LogLine
    {

        private string _thisline;
        private DateTime _timestamp;

        public string thisLine
        {
            get { return _thisline; }
            set { _thisline = value;
                   timestamp = DateTime.Now;
            }
        }

        public DateTime timestamp
        {
            get { return _timestamp; }
            set { _timestamp = value; }
        }

    }
}
