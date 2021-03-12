using Microsoft.CodeAnalysis.Editing;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    public interface IFixer
    {
        string UniqueKey
        {
            get;
        }

        string OrderingKey
        {
            get;
        }

        Task FixAsync(DocumentEditor documentEditor);
    }
}
