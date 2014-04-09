﻿// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System;

namespace InvertedSoftware.DataBlock
{
    /// <summary>
    /// Used to map query column to an object property
    /// </summary>
    public class MapToColumn : Attribute
    {
        public MapToColumn(string ColumnName)
        {
            this.ColumnName = ColumnName;
        }
        public string ColumnName { get; set; }
    }
}
