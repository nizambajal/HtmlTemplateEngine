namespace TemplateEngine.Context;

/// <summary>
/// Metadata exposed inside a foreach loop.
/// </summary>
public sealed class LoopContext
{
    public int Index { get; init; }
    public int Count { get; init; }
    public bool IsFirst => Index == 0;
    public bool IsLast => Index == Count - 1;
}

/// <summary>
/// A single frame on the context stack.
/// Each frame holds the current model object and optional loop metadata.
/// </summary>
public sealed class ContextFrame
{
    /// <summary>The model object for this scope.</summary>
    public object? Model { get; }

    /// <summary>Optional alias name when using "as" syntax in foreach.</summary>
    public string? Alias { get; }

    /// <summary>Loop metadata if this frame was created by a foreach.</summary>
    public LoopContext? LoopMeta { get; }

    public ContextFrame(object? model, string? alias = null, LoopContext? loopMeta = null)
    {
        Model = model;
        Alias = alias;
        LoopMeta = loopMeta;
    }
}

/// <summary>
/// Maintains the stack of context frames during rendering.
/// Thread-local / instance-per-render — not shared between renders.
/// </summary>
public sealed class ContextStack
{
    private readonly Stack<ContextFrame> _stack = new();

    public ContextStack(object? rootModel)
    {
        _stack.Push(new ContextFrame(rootModel));
    }

    /// <summary>The current (innermost) frame.</summary>
    public ContextFrame Current => _stack.Peek();

    /// <summary>All frames from innermost to outermost.</summary>
    public IEnumerable<ContextFrame> Frames => _stack;

    /// <summary>Total depth of the stack.</summary>
    public int Depth => _stack.Count;

    /// <summary>Pushes a new scope onto the stack.</summary>
    public void Push(object? model, string? alias = null, LoopContext? loopMeta = null)
        => _stack.Push(new ContextFrame(model, alias, loopMeta));

    /// <summary>Pops the innermost scope.</summary>
    public void Pop() => _stack.Pop();

    /// <summary>
    /// Resolves the model at a given parent-navigation depth.
    /// depth=0 means current frame, depth=1 means one level up, etc.
    /// </summary>
    public ContextFrame? FrameAt(int depth)
    {
        var frames = _stack.ToArray(); // innermost first
        return depth < frames.Length ? frames[depth] : null;
    }
}
