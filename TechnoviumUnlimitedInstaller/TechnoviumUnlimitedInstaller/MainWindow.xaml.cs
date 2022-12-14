using System;
using System.Windows;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using Microsoft.Win32;
using System.Reflection;
using System.Linq;
// to publish as one file please reffer to
// https://www.youtube.com/watch?v=QJg1ptS0At0&ab_channel=CoderFoundry
// and one bug:in csproj
// https://github.com/dotnet/wpf/issues/5909
namespace TechnoviumUnlimitedInstaller
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate
    }

    public partial class MainWindow : Window
    {
        private string rootPath;
        private string gameZip;
        private string gameExe;
        private string versionFile;

        private string UriScheme = "TechnoviumUnlimited";
        private string FriendlyName = "Technovim Unlimited";

        private LauncherStatus _status;
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        PlayButton.Content = "Play";
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Update Failed - Retry";
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Downloading Game";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Downloading Update";
                        break;
                    default:
                        break;
                }
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            SetRegister();
            CheckForUpdates();
            
        }
        

        private void SetRegister()
        {
            rootPath = Environment.ExpandEnvironmentVariables("%AppData%\\TechnoviumUnlimited");//Directory.GetCurrentDirectory();
            var key = Registry.ClassesRoot.CreateSubKey(UriScheme);
            string applicationLocation = rootPath + "\\Launcher\\TechnoviumUnlimitedLauncher.exe";
            key.SetValue("URL Protocol", "/f");
            var command = key.CreateSubKey("shell").CreateSubKey("open").CreateSubKey("command");
            command.SetValue("", applicationLocation);

            if (!Directory.Exists(rootPath))
            {
                DirectoryInfo di = Directory.CreateDirectory(rootPath);
                Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(rootPath));
            }
            versionFile = System.IO.Path.Combine(rootPath, "Version.txt");
            gameZip = System.IO.Path.Combine(rootPath, "Launcher.zip");
            gameExe = System.IO.Path.Combine(rootPath, "Launcher", "TechnoviumUnlimitedLauncher.exe");

        }

        private void CheckForUpdates()
        {
            Debug.WriteLine("------------------------> CheckForUpdates <----------------------------------");
            if (File.Exists(versionFile))
            {
                Version localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();
                try
                {
                    WebClient webClient = new WebClient();
                    Version onlineVersion = new Version(webClient.DownloadString("https://raw.githubusercontent.com/technoviumunlimited/TechnoviumUnlimitedLauncher/main/version.txt"));
                    Debug.WriteLine("CheckForUpdates onlineVersion.ToString()");
                    Debug.Print(onlineVersion.ToString());
                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.ready;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error checking for game updates: {ex}");
                }
            }
            else
            {
                InstallGameFiles(false, Version.zero);
            }
        }

        private void InstallGameFiles(bool _isUpdate, Version _onlineVersion)
        {
            
            try
            {
                WebClient webClient = new WebClient();
                if (_isUpdate)
                {
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.downloadingGame;
                    _onlineVersion = new Version(webClient.DownloadString("https://raw.githubusercontent.com/technoviumunlimited/TechnoviumUnlimitedLauncher/main/version.txt"));
                }


                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                Debug.WriteLine("_onlineVersion.ToString()");
                Debug.WriteLine(_onlineVersion.ToString());
                var update_link = "https://github.com/technoviumunlimited/TechnoviumUnlimitedLauncher/releases/download/" + _onlineVersion.ToString() + "/Launcher.zip";
                Debug.WriteLine(update_link);
                webClient.DownloadFileAsync(new Uri(update_link), gameZip, _onlineVersion);

                //webClient.DownloadFileAsync(new Uri("https://github.com/technoviumunlimited/TechnoviumUnlimitedLauncher/releases/tag/" + _onlineVersion + "/Build.zip"), gameZip, _onlineVersion);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }
        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                string onlineVersion = ((Version)e.UserState).ToString();
                ZipFile.ExtractToDirectory(gameZip, rootPath, true);
                File.Delete(gameZip);

                File.WriteAllText(versionFile, onlineVersion);

                VersionText.Text = onlineVersion;
                Status = LauncherStatus.ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing download: {ex}");
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            //never gets here, so I added to MainWindow
            CheckForUpdates();
        }


        private void Play_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = System.IO.Path.Combine(rootPath, "Launcher");
                Debug.WriteLine("startInfo.ToString():");
                Debug.WriteLine(startInfo.ToString());
                Process.Start(startInfo);
                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }
        struct Version
        {
            internal static Version zero = new Version(0, 0, 0);
            private short major;
            private short minor;
            private short subMinor;
            internal Version(short _major, short _minor, short _subMinor)
            {
                major = _major;
                minor = _minor;
                subMinor = _subMinor;
            }
            internal Version(string _version)
            {
                string[] versionStrings = _version.Split('.');
                if (versionStrings.Length != 3)
                {
                    major = 0;
                    minor = 0;
                    subMinor = 0;
                    return;
                }

                major = short.Parse(versionStrings[0]);
                minor = short.Parse(versionStrings[1]);
                subMinor = short.Parse(versionStrings[2]);
            }
            internal bool IsDifferentThan(Version _otherVersion)
            {
                if (major != _otherVersion.major)
                {
                    return true;
                }
                else
                {
                    if (minor != _otherVersion.minor)
                    {
                        return true;
                    }
                    else
                    {
                        if (subMinor != _otherVersion.subMinor)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public override string ToString()
            {
                return $"{major}.{minor}.{subMinor}";
            }


        }
    }
}

