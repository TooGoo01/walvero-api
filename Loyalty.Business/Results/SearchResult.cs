using System.Collections;

namespace Loyalty.Business.Results
{
    public class SearchResult<T> where T : IEnumerable
    {
        public int DataCount { get; set; }

        public int PageCount
        {
            get
            {
                return (int)Math.Ceiling((decimal)DataCount / PageSize);
            }
        }

        public int CurrentPage { get; set; }

        public int PageSize { get; set; }

        public int DataCountInCurrentPage
        {
            get
            {
                int cnt = 0;
                foreach (var item in Value)
                    cnt++;
                return cnt;
            }
        }

        public T Value { get; set; }
    }
}
