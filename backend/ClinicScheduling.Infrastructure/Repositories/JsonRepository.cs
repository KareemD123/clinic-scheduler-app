using ClinicScheduling.Domain.Interfaces;
using ClinicScheduling.Infrastructure.Data;

namespace ClinicScheduling.Infrastructure.Repositories;

public class JsonRepository<T> : IRepository<T> where T : class
{
    protected readonly JsonDatabase _database;
    protected List<T> Collection { get; set; }

    public JsonRepository(JsonDatabase database, List<T> collection)
    {
        _database = database;
        Collection = collection;
    }

    public Task<T?> GetByIdAsync(Guid id)
    {
        var idProperty = typeof(T).GetProperty("Id");
        var entity = Collection.FirstOrDefault(e => 
            (Guid)(idProperty?.GetValue(e) ?? Guid.Empty) == id);
        return Task.FromResult(entity);
    }

    public Task<IEnumerable<T>> GetAllAsync()
    {
        return Task.FromResult(Collection.AsEnumerable());
    }

    public async Task<T> AddAsync(T entity)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && (Guid)(idProperty.GetValue(entity) ?? Guid.Empty) == Guid.Empty)
        {
            idProperty.SetValue(entity, Guid.NewGuid());
        }

        var createdAtProperty = typeof(T).GetProperty("CreatedAt");
        createdAtProperty?.SetValue(entity, DateTime.UtcNow);

        var updatedAtProperty = typeof(T).GetProperty("UpdatedAt");
        updatedAtProperty?.SetValue(entity, DateTime.UtcNow);

        Collection.Add(entity);
        await _database.SaveData();
        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        var idProperty = typeof(T).GetProperty("Id");
        var id = (Guid)(idProperty?.GetValue(entity) ?? Guid.Empty);
        
        var index = Collection.FindIndex(e => 
            (Guid)(idProperty?.GetValue(e) ?? Guid.Empty) == id);
        
        if (index != -1)
        {
            var updatedAtProperty = typeof(T).GetProperty("UpdatedAt");
            updatedAtProperty?.SetValue(entity, DateTime.UtcNow);
            
            Collection[index] = entity;
            await _database.SaveData();
        }
        
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var idProperty = typeof(T).GetProperty("Id");
        var index = Collection.FindIndex(e => 
            (Guid)(idProperty?.GetValue(e) ?? Guid.Empty) == id);
        
        if (index == -1) return false;
        
        Collection.RemoveAt(index);
        await _database.SaveData();
        return true;
    }
}
