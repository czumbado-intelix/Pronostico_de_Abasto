using Microsoft.UI.Dispatching;

namespace PronosticosAbasto.ViewModels;

/// <summary>
/// Espera a que el operador deje de escribir antes de filtrar.
/// </summary>
/// <remarks>
/// <para>
/// Cada buscador filtraba en cada tecla: escribir un codigo de nueve digitos
/// eran nueve pasadas completas sobre decenas de miles de filas, ocho de ellas
/// para resultados que nadie iba a ver. Ahora se filtra una vez, cuando el texto
/// deja de cambiar por <see cref="Delay"/>.
/// </para>
/// <para>
/// Borrar el texto se aplica al instante (no hay nada que esperar), y Enter
/// fuerza la busqueda pendiente con <see cref="Flush"/>.
/// </para>
/// </remarks>
internal sealed class SearchDebouncer
{
    /// <summary>Lo bastante corto para sentirse inmediato, lo bastante largo para cubrir una rafaga de teclas.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(220);

    private readonly Action _run;
    private readonly DispatcherQueueTimer? _timer;
    private bool _pending;

    public SearchDebouncer(Action run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _run = run;

        // Sin cola de UI (por ejemplo, si algun dia se construye el ViewModel
        // fuera de la ventana) se degrada a filtrar al instante.
        var queue = DispatcherQueue.GetForCurrentThread();
        if (queue is not null)
        {
            _timer = queue.CreateTimer();
            _timer.Interval = Delay;
            _timer.IsRepeating = false;
            _timer.Tick += (_, _) => Flush();
        }
    }

    /// <summary>Hay una busqueda escrita que todavia no se aplico.</summary>
    public bool IsPending => _pending;

    /// <summary>El texto cambio: reinicia la espera.</summary>
    public void Schedule(string? text)
    {
        if (_timer is null || string.IsNullOrWhiteSpace(text))
        {
            RunNow();
            return;
        }

        _pending = true;
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Aplica ya la busqueda pendiente, si la hay.</summary>
    public void Flush()
    {
        if (_pending)
        {
            RunNow();
        }
    }

    /// <summary>
    /// Descarta la busqueda pendiente porque otra pasada completa del modulo ya
    /// va a leer el texto actual.
    /// </summary>
    public void Cancel()
    {
        _timer?.Stop();
        _pending = false;
    }

    private void RunNow()
    {
        Cancel();
        _run();
    }
}
