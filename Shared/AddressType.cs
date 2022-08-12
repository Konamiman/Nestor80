using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80
{
    public enum AddressType : byte
    {
        ASEG = 0,
        CSEG = 1,
        DSEG = 2,
        COMMON = 3
    }
}
