using System.Threading.Tasks;

namespace AppAmbit.Services.Interfaces;

public interface ICms
{
    ICmsQueryBuilder<T> Content<T>(string contentType) where T : class;
    Task ClearCache(string contentType);
    Task ClearAllCache();
}
