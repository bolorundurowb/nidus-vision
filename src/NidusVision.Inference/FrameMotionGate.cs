namespace NidusVision.Inference;

/// <summary>
/// Decides whether a sampled frame is worth a person-model run.
/// Each camera keeps its own scratch downsample and the downsample of the last frame that was actually inferred.
/// </summary>
public sealed class FrameMotionGate
{
    public const int Stride = 8;
    public const int Grid = 8;
    public const int MinCellSamples = 16;
    public const int LumaDelta = 24;
    public const int CellChangedPercent = 8;
    public const int GlobalChangedPercent = 2;

    public static readonly TimeSpan OpenIntervalHeartbeat = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan IdleHeartbeat = TimeSpan.FromSeconds(10);

    private readonly Dictionary<Guid, CameraMotion> _cameras = [];

    public readonly record struct ContentBounds(int X, int Y, int Width, int Height)
    {
        public static ContentBounds For(int frameWidth, int sourceWidth, int sourceHeight)
        {
            var transform = LetterboxTransform.For(sourceWidth, sourceHeight, frameWidth);
            var resizedW = Math.Max(1, (int)Math.Round(sourceWidth * transform.Gain));
            var resizedH = Math.Max(1, (int)Math.Round(sourceHeight * transform.Gain));
            return new(transform.PadX, transform.PadY, resizedW, resizedH);
        }
    }

    public ref struct SampleCursor
    {
        private readonly ContentBounds _bounds;
        private readonly int _xEnd;
        private readonly int _yEnd;
        private int _x;
        private int _y;
        private bool _started;

        internal SampleCursor(int frameWidth, int frameHeight, ContentBounds bounds)
        {
            _bounds = bounds;
            _xEnd = Math.Min(frameWidth, bounds.X + bounds.Width);
            _yEnd = Math.Min(frameHeight, bounds.Y + bounds.Height);
            _x = bounds.X;
            _y = bounds.Y;
        }

        public int X { get; private set; }

        public int Y { get; private set; }

        public int Cell { get; private set; }

        public bool MoveNext()
        {
            if (_bounds.Width <= 0 || _bounds.Height <= 0 || _y >= _yEnd)
            {
                return false;
            }

            if (_started)
            {
                _x += Stride;
                if (_x >= _xEnd)
                {
                    _x = _bounds.X;
                    _y += Stride;
                    if (_y >= _yEnd)
                    {
                        return false;
                    }
                }
            }

            _started = true;
            if (_x >= _xEnd || _y >= _yEnd)
            {
                return false;
            }

            X = _x;
            Y = _y;
            var cellX = Math.Clamp((_x - _bounds.X) * Grid / _bounds.Width, 0, Grid - 1);
            var cellY = Math.Clamp((_y - _bounds.Y) * Grid / _bounds.Height, 0, Grid - 1);
            Cell = (cellY * Grid) + cellX;
            return true;
        }
    }

    public static SampleCursor Samples(int frameWidth, int frameHeight, int sourceWidth, int sourceHeight) =>
        new(frameWidth, frameHeight, ContentBounds.For(frameWidth, sourceWidth, sourceHeight));

    public bool ShouldRunModel(
        Guid cameraId,
        ReadOnlySpan<byte> rgb24,
        int width,
        int height,
        int sourceWidth,
        int sourceHeight,
        DateTimeOffset capturedAt,
        bool presenceOpen)
    {
        if (width <= 0 || height <= 0 || sourceWidth <= 0 || sourceHeight <= 0
            || (long)width * height * 3 > rgb24.Length)
        {
            return true;
        }

        var state = StateFor(cameraId);
        var comparable = state.HasReference
            && state.Width == width
            && state.Height == height
            && state.SourceWidth == sourceWidth
            && state.SourceHeight == sourceHeight;
        var changed = Scan(state, rgb24, width, height, sourceWidth, sourceHeight, comparable);
        if (!comparable || state.ScratchCount != state.ReferenceCount || changed)
        {
            return true;
        }

        var elapsed = capturedAt - state.LastRun;
        var heartbeat = presenceOpen ? OpenIntervalHeartbeat : IdleHeartbeat;
        return elapsed <= TimeSpan.Zero || elapsed >= heartbeat;
    }

    public void Remember(
        Guid cameraId,
        int width,
        int height,
        int sourceWidth,
        int sourceHeight,
        DateTimeOffset capturedAt)
    {
        var state = StateFor(cameraId);
        if (state.Reference is null || state.Reference.Length < state.ScratchCount)
        {
            state.Reference = new byte[Math.Max(state.ScratchCount, 64)];
        }

        Buffer.BlockCopy(state.Scratch, 0, state.Reference, 0, state.ScratchCount);
        state.ReferenceCount = state.ScratchCount;
        state.Width = width;
        state.Height = height;
        state.SourceWidth = sourceWidth;
        state.SourceHeight = sourceHeight;
        state.LastRun = capturedAt;
        state.HasReference = true;
    }

    private CameraMotion StateFor(Guid cameraId)
    {
        if (!_cameras.TryGetValue(cameraId, out var state))
        {
            state = new CameraMotion();
            _cameras[cameraId] = state;
        }

        return state;
    }

    private static bool Scan(
        CameraMotion state,
        ReadOnlySpan<byte> rgb24,
        int width,
        int height,
        int sourceWidth,
        int sourceHeight,
        bool compare)
    {
        Array.Clear(state.CellChanged);
        Array.Clear(state.CellTotal);
        var cursor = Samples(width, height, sourceWidth, sourceHeight);
        var index = 0;
        var changed = 0;
        var total = 0;
        while (cursor.MoveNext())
        {
            if (index >= state.Scratch.Length)
            {
                Array.Resize(ref state.Scratch, state.Scratch.Length * 2);
            }

            var pixel = ((cursor.Y * width) + cursor.X) * 3;
            var luma = Luma(rgb24[pixel], rgb24[pixel + 1], rgb24[pixel + 2]);
            state.Scratch[index] = luma;
            if (compare && state.Reference is not null && index < state.ReferenceCount)
            {
                var cell = cursor.Cell;
                state.CellTotal[cell]++;
                total++;
                if (Math.Abs(luma - state.Reference[index]) > LumaDelta)
                {
                    state.CellChanged[cell]++;
                    changed++;
                }
            }

            index++;
        }

        state.ScratchCount = index;
        if (!compare || total == 0 || index != state.ReferenceCount)
        {
            return index != state.ReferenceCount;
        }

        if (changed * 100 >= total * GlobalChangedPercent)
        {
            return true;
        }

        for (var cell = 0; cell < state.CellTotal.Length; cell++)
        {
            var samples = state.CellTotal[cell];
            if (samples < MinCellSamples)
            {
                continue;
            }

            if (state.CellChanged[cell] * 100 >= samples * CellChangedPercent)
            {
                return true;
            }
        }

        return false;
    }

    private static byte Luma(byte red, byte green, byte blue) =>
        (byte)(((red * 54) + (green * 183) + (blue * 19)) >> 8);

    private sealed class CameraMotion
    {
        public byte[] Scratch = new byte[8192];
        public byte[]? Reference;
        public int ScratchCount;
        public int ReferenceCount;
        public int Width;
        public int Height;
        public int SourceWidth;
        public int SourceHeight;
        public DateTimeOffset LastRun;
        public bool HasReference;
        public readonly int[] CellChanged = new int[Grid * Grid];
        public readonly int[] CellTotal = new int[Grid * Grid];
    }
}
