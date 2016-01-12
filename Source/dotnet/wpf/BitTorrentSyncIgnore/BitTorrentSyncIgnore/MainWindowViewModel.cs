using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using BitTorrentSyncIgnore.Bases;
using BitTorrentSyncIgnore.Collections;
using BitTorrentSyncIgnore.Helpers.Ionic.Utils;
using BitTorrentSyncIgnore.Properties;
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

        private const string SyncFolder = ".sync";
        private const string SyncIgnoreFileName = "IgnoreList";
        private const string FilesBelowComment = "# BitTorrentSync Ignore Program - Only files below are managed#";

        public MainWindowViewModel()
        {
            ValidateIsSyncFolder();
        }

        public DelegateCommand CommandSelectPath => _commandSelectPath ?? (_commandSelectPath = new DelegateCommand(OnCommandSelectPath));

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

            if (!IsValidSyncFolder) return;

            LoadPath();
        }

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
                    var relitivePath = folder.Replace(LastPath, "");

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

                                       var ignoreFile = Path.Combine(LastPath, SyncFolder, SyncIgnoreFileName);


                                       if (File.Exists(ignoreFile))
                                       {
                                           var ignoreLines = File.ReadAllLines(ignoreFile);
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
                                                       var result = new FileContainer(x, false);
                                                       _files.Add(result);
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
