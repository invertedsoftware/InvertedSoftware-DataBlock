﻿// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InvertedSoftware.DataBlock
{
    [Flags]
    public enum CrudFieldType
    {
        Create = 0x1,
        Read = 0x2,
        Update = 0x4,
        Delete = 0x8,
        DontUse = 0x10,
        All = 0x20
    }

    /// <summary>
    /// An Attribute to be used when composing sql parameters from objects.
    /// </summary>
    public class CrudField : Attribute
    {
        public CrudFieldType UsedFor { get; set; }
    }
}
