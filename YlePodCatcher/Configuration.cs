using System;
using System.Collections.Generic;

namespace YlePodCatcher
{
    [Serializable]
    public class Configuration
    {
        public string BaseUrl { get; set; }
        public string BaseFolder { get; set; }
        public List<string> LibraryIDs { get; set; }
    }
}
