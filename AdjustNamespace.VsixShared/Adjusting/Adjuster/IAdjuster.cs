using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Adjuster
{
    public interface IAdjuster
    {
        Task<bool> AdjustAsync();
    }
}
