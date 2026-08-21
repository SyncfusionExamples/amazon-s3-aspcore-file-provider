using System.Collections.Generic;

namespace Syncfusion.EJ2.FileManager.Base
{
    /// <exclude />
    public class FileManagerResponse
    {
        public FileManagerDirectoryContent CWD { get; set; }
        public IEnumerable<FileManagerDirectoryContent> Files { get; set; }

        public ErrorDetails Error { get; set; }

        public FileDetails Details { get; set; }

    }

}