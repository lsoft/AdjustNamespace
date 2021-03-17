using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
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

        Task FixAsync(string filePath);
    }
}
