using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppAmbit;

public interface ICmsQueryBuilder<T> where T : class
{
    ICmsQueryBuilder<T> Search(string query);
    ICmsQueryBuilder<T> Equals(string field, string value);
    ICmsQueryBuilder<T> NotEquals(string field, string value);
    ICmsQueryBuilder<T> Contains(string field, string value);
    ICmsQueryBuilder<T> StartsWith(string field, string value);
    ICmsQueryBuilder<T> GreaterThan(string field, object value);
    ICmsQueryBuilder<T> GreaterThanOrEqual(string field, object value);
    ICmsQueryBuilder<T> LessThan(string field, object value);
    ICmsQueryBuilder<T> LessThanOrEqual(string field, object value);
    ICmsQueryBuilder<T> InList(string field, IEnumerable<string> values);
    ICmsQueryBuilder<T> NotInList(string field, IEnumerable<string> values);
    ICmsQueryBuilder<T> OrderByAscending(string field);
    ICmsQueryBuilder<T> OrderByDescending(string field);
    ICmsQueryBuilder<T> GetPage(int page);
    ICmsQueryBuilder<T> GetPerPage(int perPage);
    Task<List<T>> GetListAsync();
}
