using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BitTorrentSyncIgnore.Bases;
using BitTorrentSyncIgnore.Collections;
using BitTorrentSyncIgnore.Helpers.Ionic.Utils;
using BitTorrentSyncIgnore.Properties;
using FirstFloor.ModernUI.Windows.Controls;
using Prism.Commands;

namespace BitTorrentSyncIgnore
{
    public class MainWindowViewModel : ViewModelBase
    {
        public readonly SortableObservableCollection<FileContainer, IComparer<FileContainer>> _files = new SortableObservableCollection<FileContainer, IComparer<FileContainer>>(new SorterName());
        public readonly ConcurrentDictionary<string, FileContainer> _fileDic = new ConcurrentDictionary<string, FileContainer>();
        private bool _isValidSyncFolder;
        private string _lastPath = Settings.Default.LastPath;
        private bool _isBusy;
        private string _busyMessage;
        private DelegateCommand _commandSelectPath;
        private DelegateCommand _commandSaveChanges;
        private DelegateCommand _commandRefresh;
        private bool _isLinux = Settings.Default.IsLinux;
        private IComparer<FileContainer> _selectedSorter;
        private List<IComparer<FileContainer>> _sortOptions = new List<IComparer<FileContainer>>();

        private const string SyncFolder = ".sync";
        private const string SyncIgnoreFileName = "IgnoreList";
        private const string FilesBelowComment = "# BitTorrentSync Ignore Program - Only files below are managed #";

        public MainWindowViewModel()
        {
            ValidateIsSyncFolder();

            _selectedSorter = new SorterName();

            SortOptions.Add(_selectedSorter);
            SortOptions.Add(new SorterSize());
        }

        public DelegateCommand CommandSelectPath => _commandSelectPath ?? (_commandSelectPath = new DelegateCommand(OnCommandSelectPath));

        public DelegateCommand CommandSaveChanges
            => _commandSaveChanges ?? (_commandSaveChanges = new DelegateCommand(OnCommandSaveChanges, OnCommandCanSaveChanges));

        public DelegateCommand CommandRefresh
            => _commandRefresh ?? (_commandRefresh = new DelegateCommand(OnCommandRefresh, OnCanCommandRefresh));

        public List<IComparer<FileContainer>> SortOptions
        {
            get { return _sortOptions; }
            set { SetProperty(ref _sortOptions, value); }
        }

        public IComparer<FileContainer> SortOptionSelected
        {
            get { return _selectedSorter; }
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
                        var fullPath = LastPath + fileContainer.IgnorePath;
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
                        sb.Append(fileContainer.IgnorePath);
                        sb.Append(LineEnding);
                    }

                    // Add to the ignore

                    var newText = sb.ToString();
                    File.WriteAllText(IgnorePath, newText, Encoding.UTF8);

                    foreach (var fileContainer in selectedItems)
                    {
                            var fullPath = LastPath + 
                                           fileContainer.IgnorePath.Replace(
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
            get { return _isValidSyncFolder; }
            set { SetProperty(ref _isValidSyncFolder, value); }
        }

        public string LastPath
        {
            get { return _lastPath; }
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
            get { return _isLinux; }
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
            get { return _isBusy; }
            set { SetProperty(ref _isBusy, value); }
        }

        public string BusyMessage
        {
            get { return _busyMessage; }
            set { SetProperty(ref _busyMessage, value); }
        }

        private void ValidateIsSyncFolder()
        {
            // Validate the base path, 
            var path = Path.Combine(LastPath, SyncFolder);
            IsValidSyncFolder = Directory.Exists(path);

            CommandSaveChanges.RaiseCanExecuteChanged();
            CommandRefresh.RaiseCanExecuteChanged();

            if (!IsValidSyncFolder) return;

            LoadPath();
        }

        private string IgnorePath => Path.Combine(LastPath, SyncFolder, SyncIgnoreFileName);

        private async void LoadPath()
        {
            IsBusy = true;

            _fileDic.Clear();
            _files.Clear();
            var t = Task.Run(() =>
            {

                BusyMessage = "Detecting if running under windows or linux...";

                if (File.Exists(IgnorePath))
                {
                    var windowsLineEndings = IsWindowsLineEnding(IgnorePath);
                    Dispatcher.BeginInvoke((Action) (() => IsLinux = !windowsLineEndings));

                }

                BusyMessage = "Getting Folders...";
                var folders = Directory.GetDirectories(LastPath);

                foreach (var folder in folders)
                {
                    if (folder.Contains(SyncFolder)) continue;

                    _fileDic.TryAdd(folder, new FileContainer(LastPath, folder, DirectorySeperator));
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
                        var container = _fileDic.GetOrAdd(line,
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
                        foreach (var fileContainer in _fileDic.Values)
                        {
                            addDirectories(fileContainer);
                        }
                    }));
                }

            });
            await t;
            IsBusy = false;
        }

        


    }

    public class SorterName : IComparer<FileContainer>
    {
        public string Name => "Name";

        public int Compare(FileContainer x, FileContainer y)
        {
            return string.Compare(x.IgnorePath, y.IgnorePath, StringComparison.CurrentCultureIgnoreCase);
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

    [DebuggerDisplay("File = {IgnorePath}, Size={Size}")]
    public class FileContainer : ViewModelBase
    {
        private bool _isSelected;
        private int _bytes;

        public FileContainer(string basePath, string fullPath, char osDirectorySeperator)
        {
            IgnorePath = fullPath.Replace(basePath, "").Replace('/', osDirectorySeperator).Replace('\\', osDirectorySeperator);
            FullPath = fullPath;


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
        }

        public List<FileContainer> ChildFileContainers { get; }

        public string IgnorePath { get; }

        public string FullPath { get; }

        public long Size { get; set; }

        public long SizeMB => Size/1024/1024;

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }

        public int Bytes
        {
            get { return _bytes; }
            set { SetProperty(ref _bytes, value); }
        }

    }


}
