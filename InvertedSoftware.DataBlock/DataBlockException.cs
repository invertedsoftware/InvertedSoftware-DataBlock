﻿// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED.
//
// Copyright (C) Inverted Software(TM). All rights reserved.
//

using System;

namespace InvertedSoftware.DataBlock
{
    /// <summary>
    /// This exception is being thrown from the DataBlock on error.
    /// </summary>
    public class DataBlockException : Exception
    {
        public DataBlockException()
        {
        }

        public DataBlockException(string message)
            : base(message)
        {
        }

        public DataBlockException(string message, Exception inner)
            : base(message, inner)
        {
            // Log the exception
        }
    }
}