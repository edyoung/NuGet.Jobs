using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    public interface ISymbolsImporter
    {
        Task<int> Import(ISymbolPackage package);
    }
}
