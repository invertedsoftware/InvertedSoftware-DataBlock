﻿// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System.Collections.Generic;

namespace InvertedSoftware.DataBlock
{
    public class ObjectListResult<T>
    {
        public List<T> CurrentPage { get; set; }
        public int VirtualTotal { get; set; }
    }
}
