using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvertedSoftware.DataBlock
{
    public class ObjectListResult<T>
    {
        public List<T> CurrentPage { get; set; }
        public int VirtualTotal { get; set; }
    }
}
