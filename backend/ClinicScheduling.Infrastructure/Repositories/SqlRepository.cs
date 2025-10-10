using Microsoft.EntityFrameworkCore;
using ClinicScheduling.Domain.Interfaces;
using ClinicScheduling.Infrastructure.Data;
using System.Linq.Expressions;

namespace ClinicScheduling.Infrastructure.Repositories;

public class SqlRepository<T> : IRepository<T> where T : class
{
    protected readonly SqlDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public SqlRepository(SqlDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T> AddAsync(T entity)
    {
        var entry = await _dbSet.AddAsync(entity);
        return entry.Entity;
    }

    public async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
        return entities;
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    public async Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return entity;
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public void UpdateRange(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null)
            return false;
        
        _dbSet.Remove(entity);
        return true;
    }

    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    public async Task<int> CountAsync()
    {
        return await _dbSet.CountAsync();
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.CountAsync(predicate);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    // Advanced SQL operations for bulk processing
    public async Task<int> BulkUpdateAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression)
    {
        // This would use EF Core's ExecuteUpdateAsync in a real implementation
        var entities = await _dbSet.Where(predicate).ToListAsync();
        var compiled = updateExpression.Compile();
        
        foreach (var entity in entities)
        {
            var updated = compiled(entity);
            _context.Entry(entity).CurrentValues.SetValues(updated);
        }
        
        return entities.Count();
    }

    public async Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate)
    {
        // This would use EF Core's ExecuteDeleteAsync in a real implementation
        var entities = await _dbSet.Where(predicate).ToListAsync();
        _dbSet.RemoveRange(entities);
        return entities.Count();
    }
}
