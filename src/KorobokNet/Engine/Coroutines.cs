using System.Collections;

namespace KorobokNet.Engine;

// Корутины как в Unity:
//   yield return null        - следующий кадр
//   yield return 0.4f        - пауза в секундах
//   yield return IEnumerator - вложенная корутина до конца
//   yield return Func<bool>  - ждать, пока не станет true
public sealed class Coroutines
{
    private readonly List<Routine> _routines = [];

    public bool IsRunning => _routines.Count > 0;

    public Routine Start(IEnumerator routine)
    {
        var r = new Routine(routine);
        _routines.Add(r);
        return r;
    }

    public void StopAll()
    {
        foreach (var r in _routines)
        {
            r.Stop();
        }
        _routines.Clear();
    }

    public void Update(float dt)
    {
        for (var i = 0; i < _routines.Count; i++)
        {
            _routines[i].Step(dt);
        }
        _routines.RemoveAll(r => r.IsFinished);
    }

    public sealed class Routine
    {
        private readonly Stack<IEnumerator> _stack = new();
        private float _wait;
        private Func<bool>? _until;

        internal Routine(IEnumerator routine) => _stack.Push(routine);

        public bool IsFinished => _stack.Count == 0;

        public void Stop() => _stack.Clear();

        internal void Step(float dt)
        {
            if (_wait > 0)
            {
                _wait -= dt;
                if (_wait > 0)
                {
                    return;
                }
            }
            if (_until is not null)
            {
                if (!_until())
                {
                    return;
                }
                _until = null;
            }

            while (_stack.Count > 0)
            {
                var top = _stack.Peek();
                if (!top.MoveNext())
                {
                    _stack.Pop();
                    continue;
                }
                switch (top.Current)
                {
                    case null:
                        return;
                    case float seconds:
                        _wait = seconds;
                        return;
                    case double seconds:
                        _wait = (float)seconds;
                        return;
                    case IEnumerator nested:
                        _stack.Push(nested);
                        continue;
                    case Func<bool> until:
                        if (!until())
                        {
                            _until = until;
                            return;
                        }
                        continue;
                    default:
                        throw new InvalidOperationException($"Непонятное значение в корутине: {top.Current}");
                }
            }
        }
    }
}
