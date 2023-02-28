using Microsoft.Extensions.FileProviders;

namespace Mirality.WatchableValue
{
    /// <summary>Extension methods for <see cref="IFileProvider"/>.</summary>
    public static class FileProviderExtensions
    {
        /// <summary>Both gets and watches a particular file path.</summary>
        /// <param name="fileProvider">The file provider.</param>
        /// <param name="subpath">The subpath to check.</param>
        /// <returns>A watchable value consisting of the file info and a watch token (even if the file does not exist).</returns>
        public static WatchableValue<IFileInfo> WatchFileInfo(this IFileProvider fileProvider, string subpath)
        {
            return WatchableValue.Create(fileProvider.GetFileInfo(subpath), subpath, fileProvider.Watch(subpath));
        }
    }
}
