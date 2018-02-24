using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    public interface ISymbolsImporter
    {
        Task<int> BeginImport(SymbolPackage package);

        Task<IEnumerable<KeyValuePair<SymbolPackage, int>>> BeginImport(List<SymbolPackage> package);
    }
}
