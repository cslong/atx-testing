using Bookstore.Domain;
using Bookstore.Domain.Books;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Bookstore.Data.Repositories
{
    public class PaginatedList<T> : IPaginatedList<T>
    {
        private readonly List<T> _items;
        private readonly IQueryable<T> _query;
        private readonly int _pageIndex;
        private readonly int _pageSize;

        public int PageIndex { get; private set; }
        public int PageSize { get; private set; }
        public int TotalCount { get; private set; }
        public int TotalPages { get; private set; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public int Count => _items.Count;
        public bool IsReadOnly => true;

        public T this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException("PaginatedList is read-only");
        }

        public PaginatedList(IQueryable<T> query, int pageIndex, int pageSize)
        {
            _query = query;
            _pageIndex = pageIndex;
            _pageSize = pageSize;
            PageIndex = pageIndex;
            PageSize = pageSize;
            _items = new List<T>();
        }

        public async Task PopulateAsync()
        {
            TotalCount = await _query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalCount / (double)_pageSize);

            var items = await _query
                .Skip((_pageIndex - 1) * _pageSize)
                .Take(_pageSize)
                .ToListAsync();

            _items.Clear();
            _items.AddRange(items);
        }

        public IEnumerable<int> GetPageList(int pageNumber)
        {
            var start = Math.Max(1, pageNumber - 2);
            var end = Math.Min(TotalPages, pageNumber + 2);
            return Enumerable.Range(start, end - start + 1);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            throw new NotSupportedException("PaginatedList is read-only");
        }

        public void Clear()
        {
            throw new NotSupportedException("PaginatedList is read-only");
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException("PaginatedList is read-only");
        }

        public int IndexOf(T item)
        {
            return _items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException("PaginatedList is read-only");
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException("PaginatedList is read-only");
        }
    }
    public class BookRepository : IBookRepository
    {
        private readonly ApplicationDbContext dbContext;

        public BookRepository(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        async Task<Book> IBookRepository.GetAsync(int id)
        {
            return await dbContext.Book
                .Include("Genre")
                .Include("Publisher")
                .Include("BookType")
                .Include("Condition")
                .SingleAsync(x => x.Id == id);
        }

        async Task<IPaginatedList<Book>> IBookRepository.ListAsync(BookFilters filters, int pageIndex, int pageSize)
        {
            var query = dbContext.Book.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filters.Name))
            {
                query = query.Where(x => x.Name.Contains(filters.Name));
            }

            if (!string.IsNullOrWhiteSpace(filters.Author))
            {
                query = query.Where(x => x.Author.Contains(filters.Author));
            }

            if (filters.ConditionId.HasValue)
            {
                query = query.Where(x => x.ConditionId == filters.ConditionId);
            }

            if (filters.BookTypeId.HasValue)
            {
                query = query.Where(x => x.BookTypeId == filters.BookTypeId);
            }

            if (filters.GenreId.HasValue)
            {
                query = query.Where(x => x.GenreId == filters.GenreId);
            }

            if (filters.PublisherId.HasValue)
            {
                query = query.Where(x => x.PublisherId == filters.PublisherId);
            }

            if (filters.LowStock)
            {
                query = query.Where(x => x.Quantity <= Book.LowBookThreshold);
            }

            query = query
                .Include(x => x.Genre)
                .Include(x => x.Publisher)
                .Include(x => x.BookType)
                .Include(x => x.Condition);

            var result = new PaginatedList<Book>(query, pageIndex, pageSize);

            await result.PopulateAsync();

            return result;
        }

        async Task<IPaginatedList<Book>> IBookRepository.ListAsync(string searchString, string sortBy, int pageIndex, int pageSize)
        {
            var query = dbContext.Book.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(x => x.Name.Contains(searchString) ||
                                         x.Genre.Text.Contains(searchString) ||
                                         x.BookType.Text.Contains(searchString) ||
                                         x.ISBN.Contains(searchString) ||
                                         x.Publisher.Text.Contains(searchString));
            };

            switch (sortBy)
            {
                case "Name":
                    query = query.OrderBy(x => x.Name);
                    break;

                case "PriceAsc":
                    query = query.OrderBy(x => x.Price);
                    break;

                case "PriceDesc":
                    query = query.OrderByDescending(x => x.Price);
                    break;

                default:
                    query.OrderBy(x => x.Name);
                    break;
            }

            var result = new PaginatedList<Book>(query, pageIndex, pageSize);

            await result.PopulateAsync();

            return result;
        }

        async Task IBookRepository.AddAsync(Book book)
        {
            await Task.Run(() => dbContext.Book.Add(book));
        }

        async Task IBookRepository.UpdateAsync(Book book)
        {
            var existing = await dbContext.Book.FindAsync(book.Id);

            dbContext.Entry(existing).CurrentValues.SetValues(book);

            if (string.IsNullOrWhiteSpace(book.CoverImageUrl))
            {
                dbContext.Entry(existing).Property(x => x.CoverImageUrl).IsModified = false;
            }
        }

        async Task IBookRepository.SaveChangesAsync()
        {
            await dbContext.SaveChangesAsync();
        }

        async Task<BookStatistics> IBookRepository.GetStatisticsAsync()
        {
            return await dbContext.Book
                .GroupBy(x => 1)
                .Select(x => new BookStatistics
                {
                    LowStock = x.Count(y => y.Quantity > 0 && y.Quantity < Book.LowBookThreshold),
                    OutOfStock = x.Count(y => y.Quantity == 0),
                    StockTotal = x.Count()
                }).SingleOrDefaultAsync();
        }
    }
}
