using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Syncfusion.EJ2.FileManager.Base
{
    /// <exclude />
    public class FileManagerParams
    {
        public string Name { get; set; }

        public string[] Names { get; set; }

        public string Path { get; set; }

        public string TargetPath { get; set; }

        public string NewName { get; set; }

        public object Date { get; set; }

        public IEnumerable<IFormFile> FileUpload { get; set; }

        public string[] RenameFiles { get; set; }
    }
}