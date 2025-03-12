using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixArtConverter.MediaProcessing
{
    public class UniqueIndexGenerator : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private int _currentIndex = 1;

        public async Task<int> GetNextIndexAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                return _currentIndex++;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
