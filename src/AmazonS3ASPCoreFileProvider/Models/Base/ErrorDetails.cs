using System.Collections.Generic;

namespace Syncfusion.EJ2.FileManager.Base
{
    /// <exclude />
    public class ErrorDetails
    {

        public string Code { get; set; }

        public string Message { get; set; }

        public IEnumerable<string> FileExists { get; set; }
    }
}