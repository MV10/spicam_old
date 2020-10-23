using System;
using System.IO;
using System.Threading.Tasks;

namespace spicam
{
    /// <summary>
    /// Utilities for moving and encoding video files and snapshots.
    /// </summary>
    public static class FileProcessing
    {
        /// <summary>
        /// Moves a local file to the permanent storage location with a new name.
        /// </summary>
        public static void MoveVideoToStorage(string localFilename, string storageFilename)
        {
            // TODO Support optional on-the-fly encoding to MP4

            _ = Task.Run(() =>
            {
                var local = AppConfig.Get.LocalPath + localFilename;
                try
                {
                    File.Move(local, AppConfig.Get.StoragePath + storageFilename, overwrite: true);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"\nException of type {ex.GetType().Name} moving video {localFilename} to storage as {storageFilename}\n{ex.Message}");
                    if (ex.InnerException != null) Console.Write(ex.InnerException.Message);
                    Console.WriteLine($"\n{ex.StackTrace}");
                }
                finally
                {
                    // On the off chance that an exception occurred, we don't
                    // want to perpetually fill up the local storage with videos
                    File.Delete(local);
                }
            });
        }

        /// <summary>
        /// Moves all local JPEG files to the permanent storage location.
        /// </summary>
        public static void MoveSnapshotsToStorage()
        {
            if (AppConfig.Get.Recording.SnapshoutCount == 0) return;

            _ = Task.Run(() =>
            {
                try
                {
                    var files = Directory.GetFiles(AppConfig.Get.LocalPath, "*.jpg");
                    if (files.Length == 0) return;
                    foreach(var localPathname in files)
                    {
                        var storagePathname = AppConfig.Get.StoragePath + Path.GetFileName(localPathname);
                        File.Move(localPathname, storagePathname);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nException of type {ex.GetType().Name} moving snapshots to storage\n{ex.Message}");
                    if (ex.InnerException != null) Console.Write(ex.InnerException.Message);
                    Console.WriteLine($"\n{ex.StackTrace}");
                }
                finally
                {
                    // On the off chance that an exception occurred, we don't want
                    // to perpetually fill up the local storage with snapshots
                    var files = Directory.GetFiles(AppConfig.Get.LocalPath, "*.jpg");
                    if (files.Length > 0)
                    {
                        foreach(var localPathname in files)
                        {
                            File.Delete(localPathname);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// The circular buffer continuously writes frame data to local storage.
        /// When motion detection ends, this buffer is left behind.
        /// </summary>
        public static void DeleteAbandonedFiles()
        {
            _ = Task.Run(() =>
            {
                try
                {
                    DeleteFiles(AppConfig.Get.LocalPath + "*.h264");
                    DeleteFiles(AppConfig.Get.LocalPath + "*.jpg");
                }
                catch { }
            });

            static void DeleteFiles(string wildcardPathspec)
            {
                var files = Directory.GetFiles(wildcardPathspec);
                foreach (var pathname in files)
                {
                    File.Delete(pathname);
                }
            }
        }
    }
}
