using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
{
    public interface IFixer
    {
        public string FilePath
        {
            get;
        }

        Task FixAsync();
    }
}
