using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BitTorrentSyncIgnore.Bases;
using BitTorrentSyncIgnore.Collections;
using BitTorrentSyncIgnore.Helpers.Ionic.Utils;
using BitTorrentSyncIgnore.Properties;
using Prism.Commands;
using TMDbLib.Client;
using IPrompt;
using BitTorrentSyncIgnore.Extensions;
using System.Threading;
using BitTorrentSyncIgnore.Annotations;
using Newtonsoft.Json;
using TMDbLib.Objects.Search;
using RateLimiter;

namespace BitTorrentSyncIgnore
{
    public class MainWindowViewModel : ViewModelBase
    {
        public readonly SortableObservableCollection<FileContainer, IComparer<FileContainer>> _files = new SortableObservableCollection<FileContainer, IComparer<FileContainer>>(new SorterName());
        public readonly ConcurrentDictionary<string, FileContainer> FileDic = new ConcurrentDictionary<string, FileContainer>();
        private bool _isValidSyncFolder;
        private string _lastPath = Settings.Default.LastPath;
        private bool _isBusy;
        private string _busyMessage;
        private DelegateCommand _commandSelectPath;
        private DelegateCommand _commandSaveChanges;
        private DelegateCommand _commandRefresh;
        private DelegateCommand _commandScanTmdb;
        private bool _isLinux = Settings.Default.IsLinux;
        private IComparer<FileContainer> _selectedSorter = new SorterName();
        private SyncConfig _config;

        private const string SyncFolder = ".sync";
        private const string SyncIgnoreConfigFolder = "SyncConfig";
        private const string SyncConfigFile = "SyncConfig.json";
        private const string SyncIgnoreFileName = "IgnoreList";
        private const string FilesBelowComment = "# BitTorrentSync Ignore Program - Only files below are managed #";

        public MainWindowViewModel()
        {
            ValidateIsSyncFolder();

            SortOptions =  new List<IComparer<FileContainer>>
            {
                _selectedSorter,
                new SorterSize(),
                new SorterRating(),
                new SorterPopularity()
            };

        }

        private async void ProcessTmDbInfo()
        {
            try
            {
                IsBusy = true;

                BusyMessage = "Saving Config...";

                SaveConfig();

                if (string.IsNullOrWhiteSpace(Settings.Default.TMDbApiKey))
                {
                    // Prompt user for API key
                    // Prompt from : https://github.com/ramer/IPrompt
                    var prompt = IInputBox.Show("Enter your Movie Database API Key", "API Key Required");
                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        IMessageBox.Show("The Movie Database Key is required for this service. Signup for the API key (free) at www.themoviedb.org");
                        return;
                    }

                    Settings.Default.TMDbApiKey = prompt;
                    Settings.Default.Save();
                }

                // API using nuget package -- code here;  https://github.com/LordMike/TMDbLib
                var client = new TMDbClient(Settings.Default.TMDbApiKey);
                await client.AuthenticationRequestAutenticationTokenAsync();
                
                // API permits only 40 calls per 10 seconds. using this nuget package to limit it to 35.
                var timeconstraint = TimeLimiter.GetFromMaxCountByInterval(35, TimeSpan.FromSeconds(10));

                // loop over the folders and find the folders that dont have a metadata data file in them
                foreach (var f in _files.Where(x => !x.IsSelected))
                {
                    if (f.TheMovieDatabaseData != null) continue;

                    var folderName = f.LocalFolderPath.Substring(1, f.LocalFolderPath.Length -1);

                    if (folderName.Contains(DirectorySeperator)) continue;

                    var name = f.SingleDirectoryName.GetNameBeforeYear();

                    if (Config.MovieOption)
                    {
                        // Currently dont support season info lookup. Just primary anme. 
                        if (f.SingleDirectoryName.ToLower().StartsWith("season")) continue;

                        var year = f.SingleDirectoryName.GetYear();
                        if (year.HasValue)
                        {
                            BusyMessage = $"Searching For Movie '{name}' ({year.Value})";
                            await timeconstraint.Perform(async () =>
                            {
                                var result = await client.SearchMovieAsync(name, year: year.Value);
                                if (result.TotalResults >= 1)
                                {
                                    var r = result.Results[0];
                                    var json = JsonConvert.SerializeObject(r, Formatting.Indented);
                                    File.WriteAllText(f.TheMovieDatabaseFileName, json);
                                }
                            });
                        }
                    }
                    else if(Config.TvOption)
                    {
                        BusyMessage = $"Searching For Tv Show '{name}'";
                        await timeconstraint.Perform(async () =>
                        {
                            var result = await client.SearchTvShowAsync(name);
                            if (result.TotalResults >= 1)
                            {
                                var r = result.Results[0];
                                var json = JsonConvert.SerializeObject(r, Formatting.Indented);
                                File.WriteAllText(f.TheMovieDatabaseFileName, json);
                            }
                        });
                    }
                }
            }
            catch (TaskCanceledException t)
            {
                // Ignore, Task was canceled. 
            }
            catch (Exception ex)
            {
                IMessageBox.Show(ex.Message);
                if (ex.Message.Contains("unauthorized"))
                {
                    Settings.Default.TMDbApiKey = string.Empty;
                    Settings.Default.Save();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public Task MyMethod(CancellationToken t = default(CancellationToken))
        {
            return Task.FromResult(0);
        }

        public DelegateCommand CommandSelectPath => _commandSelectPath ?? (_commandSelectPath = new DelegateCommand(OnCommandSelectPath));

        public DelegateCommand CommandSaveChanges
            => _commandSaveChanges ?? (_commandSaveChanges = new DelegateCommand(OnCommandSaveChanges, OnCommandCanSaveChanges));

        public DelegateCommand CommandRefresh
            => _commandRefresh ?? (_commandRefresh = new DelegateCommand(OnCommandRefresh, OnCanCommandRefresh));

        public List<IComparer<FileContainer>> SortOptions { get; }

        public IComparer<FileContainer> SortOptionSelected
        {
            get => _selectedSorter;
            set
            {
                if (SetProperty(ref _selectedSorter, value))
                {
                    Files.ChangeSort(value);
                }
            }
        }

        private void OnCommandRefresh()
        {
            ValidateIsSyncFolder();
        }

        private bool OnCanCommandRefresh()
        {
            return IsValidSyncFolder;
        }

        public SortableObservableCollection<FileContainer, IComparer<FileContainer>> Files => _files;

        private void OnCommandSelectPath()
        {
            var dialog = new  FolderBrowserDialogEx();
            dialog.ShowNewFolderButton = false;
            dialog.SelectedPath = LastPath;
            dialog.ShowFullPathInEditBox = true;
            dialog.ShowEditBox = true;
            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                LastPath = dialog.SelectedPath;
            }
        }

        private bool OnCommandCanSaveChanges()
        {
            return IsValidSyncFolder;
        }

        private async void OnCommandSaveChanges()
        {
            var t = Task.Run(() =>
            {

                try
                {

                    IsBusy = true;
                    // Rebuild the exclude list here
                    var selectedItems = Files.Where(x => x.IsSelected).ToList();

                    // Tell the user the changes that will result in a delete, and how much storage it will make
                    long storageSaved = 0;
                    int filesRemoved = 0;
                    BusyMessage = $"Building File List";
                    foreach (var fileContainer in selectedItems)
                    {
                        var fullPath = LastPath + fileContainer.LocalFolderPath;
                        BusyMessage = $"Scanning for files and folders in: {fullPath}";
                        if (Directory.Exists(fullPath))
                        {
                            var files = Directory.GetFiles(fullPath, "*.*",
                                SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                filesRemoved++;
                                var info = new FileInfo(file);
                                storageSaved += info.Length;
                            }
                        }
                    }

                    if (filesRemoved > 0)
                    {
                        // prompt the user

                    }


                    var sb = new StringBuilder();

                    BusyMessage = $"Updating IgnoreList..";
                    // Identify the text before the FilesBelowComment section, if one exists
                    if (File.Exists(IgnorePath))
                    {
                        var ignoreLines = File.ReadAllLines(IgnorePath);
                        foreach (var ignoreLine in ignoreLines)
                        {
                            if (ignoreLine == LineEnding) continue; // skip any extra empty lines.

                            if (ignoreLine.Contains(FilesBelowComment))
                            {
                                break;
                            }
                            sb.Append(ignoreLine);
                            sb.Append(LineEnding);
                        }
                    }

                    sb.Append(FilesBelowComment);
                    sb.Append(LineEnding);

                    foreach (var fileContainer in selectedItems)
                    {
                        sb.Append(fileContainer.LocalFolderPath);
                        sb.Append(LineEnding);
                    }

                    // Add to the ignore

                    var newText = sb.ToString();
                    File.WriteAllText(IgnorePath, newText, Encoding.UTF8);

                    foreach (var fileContainer in selectedItems)
                    {
                            var fullPath = LastPath + 
                                           fileContainer.LocalFolderPath.Replace(
                                               DirectorySeperator, Path.DirectorySeparatorChar);
                            if (Directory.Exists(fullPath))
                            {
                                BusyMessage = $"Deleting folder {fullPath}";
                                DeleteRecursiveFolder(fullPath);
                            }

                    }

                    IsBusy = false;
                }
                catch (Exception ex)
                {
                    BusyMessage = $"Error removing a folder: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            });
            await t;

        }

        /// <summary>
        /// Remove folder, as well as dropping any special file attribute
        /// http://stackoverflow.com/questions/611921/how-do-i-delete-a-directory-with-read-only-files-in-c
        /// </summary>
        /// <param name="fileSystemInfo"></param>
        private void DeleteRecursiveFolder(string pFolderPath)
        {
            foreach (string Folder in Directory.GetDirectories(pFolderPath))
            {
                DeleteRecursiveFolder(Folder);
            }

            foreach (string file in Directory.GetFiles(pFolderPath))
            {
                var pPath = Path.Combine(pFolderPath, file);
                FileInfo fi = new FileInfo(pPath);
                File.SetAttributes(pPath, FileAttributes.Normal);
                File.Delete(file);
            }

            Directory.Delete(pFolderPath);
        }

        /// <summary>
        /// To auto detect, 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="newLine"></param>
        /// <returns></returns>
        public bool IsWindowsLineEnding(string path)
        {
            using (var fileStream = File.OpenRead(path))
            {
                char prevChar = '\0';

                // Read the first 4000 characters to try and find a newline
                for (int i = 0; i < 4000; i++)
                {
                    int b;
                    if ((b = fileStream.ReadByte()) == -1) break;

                    char curChar = (char)b;

                    if (curChar == '\n')
                    {
                        return prevChar == '\r';
                    }

                    prevChar = curChar;
                }

                return false;
            }
        }

        /// <summary>
        /// When the process detects that the path is valid, this will be true.
        /// </summary>
        public bool IsValidSyncFolder
        {
            get => _isValidSyncFolder;
            set => SetProperty(ref _isValidSyncFolder, value);
        }

        public string LastPath
        {
            get => _lastPath;
            set
            {
                if (SetProperty(ref _lastPath, value))
                {
                    Settings.Default.LastPath = value;
                    Settings.Default.Save();
                    ValidateIsSyncFolder();
                }
            }
        }

        public bool IsLinux
        {
            get => _isLinux;
            set {
                if (SetProperty(ref _isLinux, value))
                {
                    Settings.Default.IsLinux = value;
                    Settings.Default.Save();
                    ValidateIsSyncFolder();
                }
            }
        }

        private char DirectorySeperator => IsLinux ? '/' : '\\';

        private string LineEnding => IsLinux ? "\n" : "\r\n";

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }

        private void ValidateIsSyncFolder()
        {
            // Validate the base path, 
            var path = Path.Combine(LastPath, SyncFolder);
            IsValidSyncFolder = Directory.Exists(path);

            CommandSaveChanges.RaiseCanExecuteChanged();
            CommandRefresh.RaiseCanExecuteChanged();

            if (!IsValidSyncFolder) return;

            LoadConfig();

            LoadPath();
        }

        private string IgnorePath => Path.Combine(LastPath, SyncFolder, SyncIgnoreFileName);

        private async void LoadPath()
        {
            IsBusy = true;

            FileDic.Clear();
            _files.Clear();
            var t = Task.Run(() =>
            {

                BusyMessage = "Detecting if running under windows or linux...";

                if (File.Exists(IgnorePath))
                {
                    var windowsLineEndings = IsWindowsLineEnding(IgnorePath);
                    Dispatcher.BeginInvoke((Action) (() => IsLinux = !windowsLineEndings));

                }

                BusyMessage = "Reading Config...";

                BusyMessage = "Getting Folders...";
                var folders = Directory.GetDirectories(LastPath);

                for (var index = 0; index < folders.Length; index++)
                {
                    var folder = folders[index];
                    if (folder.Contains(SyncFolder)) continue;

                    var directory = new DirectoryInfo(folder).Name;
                    // Dont show hidden folders such as the .stream folder. 
                    if (directory.StartsWith(".")) continue;

                    // We will have some config data in this folder.  This will be synced with the users, but just now givin the option to remove. 
                    if (directory == SyncIgnoreConfigFolder) continue;

                    BusyMessage = $"Processing Folder {index + 1} / {folders.Length}: {directory}";

                    FileDic.TryAdd(folder, new FileContainer(LastPath, folder, DirectorySeperator));
                }

                BusyMessage = "Reading Ignore File...";


                if (File.Exists(IgnorePath))
                {
                    var ignoreLines = File.ReadAllLines(IgnorePath);
                    bool hitIgnore = false;
                    foreach (var line in ignoreLines)
                    {
                        if (line.Contains(FilesBelowComment) && hitIgnore == false)
                        {
                            hitIgnore = true;
                            continue;
                        }

                        if (hitIgnore == false) continue;

                        // If we are here, we have hit the line and can start processing

                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.Contains(SyncFolder))
                            continue;

                        // TODO: store in comment if folder or file.
                        var container = FileDic.GetOrAdd(line,
                            (path) =>
                            {
                                var adjustedPath = path.Replace('/', Path.DirectorySeparatorChar)
                                    .Replace('\\', Path.DirectorySeparatorChar);
                                var fullPath = LastPath + adjustedPath;

                                var result = new FileContainer(LastPath, fullPath, DirectorySeperator);
                                
                                return result;
                            });

                        container.IsSelected = true;

                    }

                    Action<FileContainer> addDirectories = null;
                    addDirectories = ((fileC) =>
                    {
                        _files.Add(fileC);

                        foreach (var childC in fileC.ChildFileContainers)
                        {
                            addDirectories(childC);
                        }
                    });
                    
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        foreach (var fileContainer in FileDic.Values)
                        {
                            addDirectories(fileContainer);
                        }
                    }));
                }

            });
            await t;
            IsBusy = false;
        }

        public SyncConfig Config
        {
            get { return _config; }
            set { SetProperty(ref _config, value); }
        }

        private void LoadConfig()
        {
            if(Config != null)
            {
                Config.PropertyChanged -= Config_PropertyChanged;
            }

            // Loading Config
            var configPath = ConfigPath();

            if (File.Exists(configPath))
            {
                try
                {
                    var fileText = File.ReadAllText(configPath);
                    Config = JsonConvert.DeserializeObject<SyncConfig>(fileText);
                }
                catch(Exception ex)
                {
                    IMessageBox.Show("Can't load config. Error: " + ex.Message);
                }
            }
            
            if(Config == null) Config = new SyncConfig();


            Config.PropertyChanged += Config_PropertyChanged;
        }

        public void SaveConfig()
        {
            var configPath = ConfigPath();
            try
            {
                var text = JsonConvert.SerializeObject(Config);

                var directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(configPath, text);
            }
            catch(Exception ex)
            {
                IMessageBox.Show("Can't save config. Error: " + ex.Message);
            }
        }

        private void Config_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "MovieOption":
                case "TvOption":
                    ProcessTmDbInfo();
                    break;
            }
        }

        private string ConfigPath()
        {
            return Path.Combine(LastPath, SyncIgnoreConfigFolder, SyncConfigFile);
        }

        public DelegateCommand CommandScanTmdbChanges => _commandScanTmdb ?? (_commandScanTmdb = new DelegateCommand(ProcessTmDbInfo));
    }

    public class SorterTmdb : IComparer<FileContainer>
    {
        public string Name => "Name";

        public int Compare(FileContainer x, FileContainer y)
        {
            return string.Compare(x.LocalFolderPath, y.LocalFolderPath, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    public class SorterName : IComparer<FileContainer>
    {
        public string Name => "Name";

        public int Compare(FileContainer x, FileContainer y)
        {
            return string.Compare(x.LocalFolderPath, y.LocalFolderPath, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    public class SorterSize : IComparer<FileContainer>
    {
        public string Name => "Size";

        public int Compare(FileContainer x, FileContainer y)
        {
            return y.Size.CompareTo(x.Size);
        }
    }

    public class SorterRating : IComparer<FileContainer>
    {
        public string Name => "Rating";

        public int Compare(FileContainer x, FileContainer y)
        {
            return y.VoteAverage.CompareTo(x.VoteAverage);
        }
    }

    public class SorterPopularity : IComparer<FileContainer>
    {
        public string Name => "Popularity";

        public int Compare(FileContainer x, FileContainer y)
        {
            return y.VoteCounts.CompareTo(x.VoteCounts);
        }
    }

    [DebuggerDisplay("File = {LocalFolderPath}, Size={Size}")]
    public class FileContainer : ViewModelBase
    {
        private bool _isSelected;
        private int _bytes;

        public FileContainer(string basePath, string fullPath, char osDirectorySeperator)
        {
            LocalFolderPath = fullPath.Replace(basePath, "").Replace('/', osDirectorySeperator).Replace('\\', osDirectorySeperator);
            FullPath = fullPath;
            SingleDirectoryName = new DirectoryInfo(FullPath).Name;

            long size = 0;
            // get sub directories here
            if (Directory.Exists(FullPath))
            {
                var subDirs = Directory.GetDirectories(FullPath);

                ChildFileContainers =
                    subDirs.Select(path => new FileContainer(basePath, path, osDirectorySeperator)).ToList();

                var files = Directory.GetFiles(FullPath);
                size = files.Select(file => new FileInfo(file)).Select(fileInfo => fileInfo.Length).Sum();
            }
            else
            {
                ChildFileContainers = new List<FileContainer>();
            }

            var subSizes = ChildFileContainers.Select(x => x.Size).Sum();

            Size = size + subSizes;

            TheMovieDatabaseFileName = Path.Combine(fullPath, SingleDirectoryName) + ".tmdb";
            if (File.Exists(TheMovieDatabaseFileName))
            {
                try
                {
                    TheMovieDatabaseData = JsonConvert.DeserializeObject<SearchMovie>(File.ReadAllText(TheMovieDatabaseFileName));
                }
                catch
                {
                    // Ignore
                }
            }
        }

        public List<FileContainer> ChildFileContainers { get; }

        public string LocalFolderPath { get; }

        public string FullPath { get; }

        public string SingleDirectoryName { get; }

        public long Size { get; set; }

        public long SizeMB => Size/1024/1024;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int Bytes
        {
            get => _bytes;
            set => SetProperty(ref _bytes, value);
        }

        public double VoteAverage => TheMovieDatabaseData?.VoteAverage ?? 0.0d;

        public double VoteCounts => TheMovieDatabaseData?.VoteCount ?? 0.0d;

        public SearchMovie TheMovieDatabaseData { get; set; }

        public string TheMovieDatabaseFileName { get; }
    }

    public abstract class TypeOptionsBase : ViewModelBase
    {
        bool _isSelected;

        public abstract string Name { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class SyncConfig : INotifyPropertyChanged
    {
        private bool _movieOption;
        private bool _tvOption;

        public bool MovieOption
        {
            get => _movieOption;
            set
            {
                _movieOption = value;
                OnPropertyChanged();
            }
        }

        public bool TvOption
        {
            get => _tvOption;
            set
            {
                _tvOption = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


}
