﻿// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System;

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
    /// Indicates the CRUD operation this object property is to be used for.
    /// </summary>
    public class CrudField : Attribute
    {
        public CrudFieldType UsedFor { get; set; }
    }
}