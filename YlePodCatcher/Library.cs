﻿using System.Collections.Generic;

namespace YlePodCatcher
{
    public class Library
    {
        public string LibraryID; // Name in checkbox list items
        public string Title;
        public bool HasFolder;
        public IList<Mp3File> Mp3Files;
    }
}
