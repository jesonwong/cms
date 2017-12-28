using System;
using System.IO;
using BaiRong.Core.Model.Enumerations;

namespace BaiRong.Core.IO
{
    public class FileWatcherClass
    {
        public const string Node = nameof(Node);
        public const string PublishmentSystem = nameof(PublishmentSystem);
        public const string TableMetadata = nameof(TableMetadata);
        public const string TableColumn = nameof(TableColumn);
        public const string ServiceStatus = nameof(ServiceStatus);
        public const string ServiceTask = nameof(ServiceTask);
        public const string ServiceIsPendingCreate = nameof(ServiceIsPendingCreate);
        public const string Plugin = nameof(Plugin);

        private FileSystemWatcher _fileSystemWatcher;
        private readonly string _cacheFilePath;

        public FileWatcherClass(string cacheName)
        {
            _cacheFilePath = PathUtils.Combine(WebConfigUtils.PhysicalApplicationPath, DirectoryUtils.SiteFiles.DirectoryName, DirectoryUtils.SiteFiles.TemporaryFiles, "Cache", $"{cacheName}.txt");

            FileDependency();
        }

        public void UpdateCacheFile()
        {
            FileUtils.WriteText(_cacheFilePath, ECharset.utf_8, "cache chaged:" + DateUtils.GetDateAndTimeString(DateTime.Now));
        }

        public void DeleteCacheFile()
        {
            FileUtils.DeleteFileIfExists(_cacheFilePath);
        }

        public delegate void FileChange(object sender, EventArgs e);

        //The OnFileChange event is fired when the file changes.
        public event FileChange OnFileChange;

        public event FileChange OnFileDeleted;

        private void FileDependency()
        {
            //Validate file.
            var fileInfo = new FileInfo(_cacheFilePath);
            if (!fileInfo.Exists)
            {
                FileUtils.WriteText(_cacheFilePath, ECharset.utf_8, string.Empty);
            }

            //Get path from full file name.
            var path = Path.GetDirectoryName(_cacheFilePath);

            //Get file name from full file name.
            var fileName = Path.GetFileName(_cacheFilePath);

            //Initialize new FileSystemWatcher.
            _fileSystemWatcher = new FileSystemWatcher
            {
                Path = path,
                Filter = fileName,
                EnableRaisingEvents = true
            };
            _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            _fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            OnFileChange?.Invoke(sender, e);
        }

        private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            OnFileDeleted?.Invoke(sender, e);
        }
    }
}
