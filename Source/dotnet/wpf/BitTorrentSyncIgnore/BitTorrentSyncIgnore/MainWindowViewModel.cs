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
        public readonly SortableObservableCollection<FileContainer, IComparer<FileContainer>> _files = new SortableObservableCollection<FileContainer, IComparer<FileContainer>>(new Sorter());
        public readonly ConcurrentDictionary<string, FileContainer> _fileDic = new ConcurrentDictionary<string, FileContainer>();
        private bool _isValidSyncFolder;
        private string _lastPath = Settings.Default.LastPath;
        private bool _isBusy;
        private string _busyMessage;
        private DelegateCommand _commandSelectPath;
        private DelegateCommand _commandSaveChanges;

        private const string SyncFolder = ".sync";
        private const string SyncIgnoreFileName = "IgnoreList";
        private const string FilesBelowComment = "# BitTorrentSync Ignore Program - Only files below are managed #";

        public MainWindowViewModel()
        {
            ValidateIsSyncFolder();
        }

        public DelegateCommand CommandSelectPath => _commandSelectPath ?? (_commandSelectPath = new DelegateCommand(OnCommandSelectPath));

        public DelegateCommand CommandSaveChanges
            => _commandSaveChanges ?? (_commandSaveChanges = new DelegateCommand(OnCommandSaveChanges, OnCommandCanSaveChanges));

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
            

            var t = Task.Run(() => {

                                       try
                                       {

                                           IsBusy = true;
                                           // Rebuild the exclude list here
                                           var selectedItems = Files.Where(x => x.IsSelected).ToList();

                                           // Tell the user the changes that will result in a delete, and how much storage it will make
                                           long storageSaved = 0;
                                           int filesRemoved = 0;
                                           foreach (var fileContainer in selectedItems)
                                           {
                                               var fullPath = LastPath + fileContainer.RelitivePath;
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

                                           // Identify the text before the FilesBelowComment section, if one exists
                                           if (File.Exists(IgnorePath))
                                           {
                                               var ignoreLines = File.ReadAllLines(IgnorePath);
                                               foreach (var ignoreLine in ignoreLines)
                                               {
                                                   if (ignoreLine == "\n") continue; // skip any extra empty lines.

                                                   if (ignoreLine.Contains(FilesBelowComment))
                                                   {
                                                       break;
                                                   }
                                                   sb.Append(ignoreLine);
                                                   sb.Append("\n");
                                               }
                                           }

                                           sb.Append(FilesBelowComment);
                                           sb.Append("\n");

                                           foreach (var fileContainer in selectedItems)
                                           {
                                               sb.Append(fileContainer.RelitivePath);
                                               sb.Append("\n");
                                           }

                                           // Add to the ignore

                                           var newText = sb.ToString();
                                           File.WriteAllText(IgnorePath, newText, Encoding.UTF8);

                                           foreach (var fileContainer in selectedItems)
                                           {
                                               // Purge the directory, now that it exists
                                               if (fileContainer.IsFolder)
                                               {
                                                   var fullPath = LastPath + Path.DirectorySeparatorChar +
                                                                  fileContainer.RelitivePath;
                                                   if (Directory.Exists(fullPath))
                                                   {
                                                       BusyMessage = $"Deleting folder {fullPath}";
                                                       Directory.Delete(fullPath, true);
                                                   }
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

            if (!IsValidSyncFolder) return;

            LoadPath();
        }

        private string IgnorePath => Path.Combine(LastPath, SyncFolder, SyncIgnoreFileName);

        private async void LoadPath()
        {
            IsBusy = true;

            _fileDic.Clear();
            _files.Clear();
            var t = Task.Run(() => { 
                BusyMessage = "Getting Folders...";
                var folders = Directory.GetDirectories(LastPath, "*.*", SearchOption.AllDirectories);

                foreach (var folder in folders)
                {
                    var relitivePath = folder.Replace(LastPath + Path.DirectorySeparatorChar, "");

                    if (relitivePath.Contains(SyncFolder)) continue;

                    var container = _fileDic.GetOrAdd(relitivePath, (x) => new FileContainer(x, true));

                    Dispatcher.BeginInvoke((Action)(() => _files.Add(container)));
                }

                BusyMessage = "Getting Files...";
                //var files = Directory.GetFiles(LastPath, "*.*", SearchOption.AllDirectories);

                //foreach (var file in files)
                //{
                //    var relitivePath = file.Replace(LastPath, "");

                //    if (relitivePath.Contains(SyncFolder)) continue;

                //    var container = _fileDic.GetOrAdd(relitivePath, (x) => new FileContainer(x, false));

                //    Dispatcher.BeginInvoke((Action)(() => _files.Add(container)));
                //}

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

                                               if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.Contains(SyncFolder)) continue;

                                               // TODO: store in comment if folder or file.
                                               var container = _fileDic.GetOrAdd(line,
                                                   (x) =>
                                                   {
                                                       var result = new FileContainer(x, true);
                                                       Dispatcher.BeginInvoke((Action)(() => _files.Add(result)));
                                                       return result;
                                                   });

                                               container.IsSelected = true;

                                           }
                                       }

            });
            await t;
            IsBusy = false;
        }

        private class Sorter : IComparer<FileContainer>
        {
            public int Compare(FileContainer x, FileContainer y)
            {
                return string.Compare(x.RelitivePath, y.RelitivePath, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        
    }

    [DebuggerDisplay("File = {RelitivePath}")]
    public class FileContainer : ViewModelBase
    {
        private readonly bool _isFolder;
        private bool _isSelected;
        private int _bytes;

        public FileContainer(string relitivePath, bool isFolder)
        {
            _isFolder = isFolder;
            RelitivePath = relitivePath;
        }

        public string RelitivePath { get; private set; }

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

        public bool IsFolder
        {
            get { return _isFolder; }
        }
    }


}
