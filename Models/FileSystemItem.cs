using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebDirectory.Models
{
    public class FileSystemItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public string FormattedSize { get; set; }
        public int Level { get; set; }
        public bool IsOpen { get; set; }
        public List<FileSystemItem> SubItems { get; set; }
    }

}
