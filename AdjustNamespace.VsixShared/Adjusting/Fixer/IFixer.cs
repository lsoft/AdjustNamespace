using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
{
    public interface IFixer
    {

        void AddSubject(object o);

        Task FixAsync(string filePath);
    }
}
