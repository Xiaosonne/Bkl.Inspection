using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using System.Linq.Expressions;
using System.Linq;

namespace Bkl.Models.MongoEntity
{
    public class MongoDbSet<T> where T : MdEntityBase
    {
        private readonly IMongoCollection<T> _collection;

        public MongoDbSet(IMongoCollection<T> collection)
        {

            _collection = collection;
        }

        public async Task<List<T>> GetAsync() =>
            await _collection.Find(_ => true).ToListAsync();


        public async Task<List<T>> GetAsync(Expression<Func<T, bool>> predict) =>
            await _collection.Find(predict).ToListAsync();

        public PagedDataResponse<List<T>> GetPaged(Expression<Func<T, bool>> predict, int page, int pagesize)
        {
            var where = _collection.AsQueryable().Where(predict).OrderByDescending(s=>s.Createtime);
            return new PagedDataResponse<List<T>>
            {
                data = where.Skip(page * pagesize).Take(pagesize).ToList(),
                page=page,
                pageSize=pagesize,
                totalCount = where.Count(),
            };
        }

        public async Task<T?> GetAsync(string id) =>
            await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task CreateAsync(T newBook) =>
            await _collection.InsertOneAsync(newBook);

        public async Task CreateAsync(IEnumerable<T> newBook) =>
            await _collection.InsertManyAsync(newBook);

        public async Task UpdateAsync(string id, T updatedBook) =>
            await _collection.ReplaceOneAsync(x => x.Id == id, updatedBook);

        public async Task RemoveAsync(string id) =>
            await _collection.DeleteOneAsync(x => x.Id == id);
    }
}
