using System.Collections.Concurrent;
using System.Drawing;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.WindowsAPI;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Batched window operations service to reduce Win32 API call frequency and improve performance
/// </summary>
public interface IBatchedWindowOperations : IDisposable
{
    /// <summary>
    /// Queues a window move operation to be executed in the next batch
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    /// <param name="x">New X coordinate</param>
    /// <param name="y">New Y coordinate</param>
    /// <returns>Task that completes when the operation is executed</returns>
    Task<bool> QueueMoveWindowAsync(IntPtr windowHandle, int x, int y);

    /// <summary>
    /// Queues a window bounds retrieval operation
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    /// <returns>Task that returns the window bounds</returns>
    Task<Rectangle> QueueGetWindowBoundsAsync(IntPtr windowHandle);

    /// <summary>
    /// Forces immediate execution of all queued operations
    /// </summary>
    Task FlushOperationsAsync();
}

/// <summary>
/// Batched window operations implementation that reduces Win32 API call frequency
/// </summary>
public sealed class BatchedWindowOperations : IBatchedWindowOperations
{
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<IWindowOperation> _operationQueue = new();
    private readonly System.Threading.Timer _batchTimer;
    private readonly SemaphoreSlim _batchSemaphore = new(1, 1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private volatile bool _disposed = false;

    private const int BatchIntervalMs = 8; // ~120fps batch processing
    private const int MaxBatchSize = 50; // Maximum operations per batch

    /// <summary>
    /// Creates a new batched window operations service
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public BatchedWindowOperations(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Initialize batch processing timer
        _batchTimer = new System.Threading.Timer(ProcessBatch, null, BatchIntervalMs, BatchIntervalMs);
        
        _logger.LogDebug("BatchedWindowOperations initialized with {BatchInterval}ms intervals", BatchIntervalMs);
    }

    /// <summary>
    /// Queues a window move operation to be executed in the next batch
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    /// <param name="x">New X coordinate</param>
    /// <param name="y">New Y coordinate</param>
    /// <returns>Task that completes when the operation is executed</returns>
    public Task<bool> QueueMoveWindowAsync(IntPtr windowHandle, int x, int y)
    {
        if (_disposed)
            return Task.FromResult(false);

        var operation = new MoveWindowOperation(windowHandle, x, y, _logger);
        _operationQueue.Enqueue(operation);
        
        return operation.CompletionTask;
    }

    /// <summary>
    /// Queues a window bounds retrieval operation
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    /// <returns>Task that returns the window bounds</returns>
    public Task<Rectangle> QueueGetWindowBoundsAsync(IntPtr windowHandle)
    {
        if (_disposed)
            return Task.FromResult(Rectangle.Empty);

        var operation = new GetWindowBoundsOperation(windowHandle, _logger);
        _operationQueue.Enqueue(operation);
        
        return operation.CompletionTask;
    }

    /// <summary>
    /// Forces immediate execution of all queued operations
    /// </summary>
    public async Task FlushOperationsAsync()
    {
        if (_disposed) return;

        await _batchSemaphore.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            await ProcessBatchInternal();
        }
        finally
        {
            _batchSemaphore.Release();
        }
    }

    /// <summary>
    /// Timer callback for batch processing
    /// </summary>
    private async void ProcessBatch(object? state)
    {
        if (_disposed) return;

        if (_batchSemaphore.Wait(0)) // Non-blocking wait
        {
            try
            {
                await ProcessBatchInternal();
            }
            finally
            {
                _batchSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Internal batch processing logic
    /// </summary>
    private async Task ProcessBatchInternal()
    {
        if (_operationQueue.IsEmpty) return;

        var operations = new List<IWindowOperation>();
        var processedCount = 0;

        // Dequeue operations for this batch
        while (_operationQueue.TryDequeue(out var operation) && processedCount < MaxBatchSize)
        {
            operations.Add(operation);
            processedCount++;
        }

        if (operations.Count == 0) return;

        // Group operations by type for optimal batching
        var moveOperations = operations.OfType<MoveWindowOperation>().ToList();
        var boundsOperations = operations.OfType<GetWindowBoundsOperation>().ToList();

        // Execute batched operations on background thread
        await Task.Run(() =>
        {
            // Process move operations first (they may affect bounds operations)
            foreach (var moveOp in moveOperations)
            {
                try
                {
                    moveOp.Execute();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing batched move operation for window {Handle:X}", 
                        moveOp.WindowHandle.ToInt64());
                    moveOp.SetResult(false);
                }
            }

            // Process bounds operations
            foreach (var boundsOp in boundsOperations)
            {
                try
                {
                    boundsOp.Execute();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing batched bounds operation for window {Handle:X}", 
                        boundsOp.WindowHandle.ToInt64());
                    boundsOp.SetResult(Rectangle.Empty);
                }
            }
        });

        if (operations.Count > 1)
        {
            _logger.LogDebug("Processed batch of {Count} window operations ({Moves} moves, {Bounds} bounds)",
                operations.Count, moveOperations.Count, boundsOperations.Count);
        }
    }

    /// <summary>
    /// Disposes the batched operations service
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // Stop the batch timer
        _batchTimer?.Dispose();

        // Process any remaining operations
        try
        {
            var remainingOps = new List<IWindowOperation>();
            while (_operationQueue.TryDequeue(out var op))
            {
                remainingOps.Add(op);
            }

            // Cancel remaining operations
            foreach (var op in remainingOps)
            {
                if (op is MoveWindowOperation moveOp)
                    moveOp.SetResult(false);
                else if (op is GetWindowBoundsOperation boundsOp)
                    boundsOp.SetResult(Rectangle.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error during BatchedWindowOperations disposal: {Message}", ex.Message);
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _batchSemaphore?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Interface for window operations
    /// </summary>
    private interface IWindowOperation
    {
        IntPtr WindowHandle { get; }
    }

    /// <summary>
    /// Move window operation
    /// </summary>
    private sealed class MoveWindowOperation : IWindowOperation
    {
        private readonly TaskCompletionSource<bool> _completionSource = new();
        private readonly ILogger _logger;

        public IntPtr WindowHandle { get; }
        public int X { get; }
        public int Y { get; }
        public Task<bool> CompletionTask => _completionSource.Task;

        public MoveWindowOperation(IntPtr windowHandle, int x, int y, ILogger logger)
        {
            WindowHandle = windowHandle;
            X = x;
            Y = y;
            _logger = logger;
        }

        public void Execute()
        {
            try
            {
                // Get current bounds to preserve size
                if (User32.GetWindowRect(WindowHandle, out var rect))
                {
                    var currentBounds = rect.ToRectangle();
                    var success = User32.SetWindowPos(
                        WindowHandle,
                        IntPtr.Zero,
                        X, Y,
                        currentBounds.Width, currentBounds.Height,
                        WindowsStructures.SWP_NOZORDER | WindowsStructures.SWP_NOACTIVATE | WindowsStructures.SWP_NOREDRAW
                    );
                    SetResult(success);
                }
                else
                {
                    SetResult(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batched move operation");
                SetResult(false);
            }
        }

        public void SetResult(bool result)
        {
            _completionSource.TrySetResult(result);
        }
    }

    /// <summary>
    /// Get window bounds operation
    /// </summary>
    private sealed class GetWindowBoundsOperation : IWindowOperation
    {
        private readonly TaskCompletionSource<Rectangle> _completionSource = new();
        private readonly ILogger _logger;

        public IntPtr WindowHandle { get; }
        public Task<Rectangle> CompletionTask => _completionSource.Task;

        public GetWindowBoundsOperation(IntPtr windowHandle, ILogger logger)
        {
            WindowHandle = windowHandle;
            _logger = logger;
        }

        public void Execute()
        {
            try
            {
                if (User32.GetWindowRect(WindowHandle, out var rect))
                {
                    SetResult(rect.ToRectangle());
                }
                else
                {
                    SetResult(Rectangle.Empty);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batched bounds operation");
                SetResult(Rectangle.Empty);
            }
        }

        public void SetResult(Rectangle result)
        {
            _completionSource.TrySetResult(result);
        }
    }
}