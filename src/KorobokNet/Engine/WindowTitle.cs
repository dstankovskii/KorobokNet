using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;

namespace KorobokNet.Engine;

/// <summary>
/// MonoGame DesktopGL передаёт заголовок окна в SDL в ANSI-кодировке, и кириллица превращается в "???".
/// Передаём строку в SDL напрямую в UTF-8.
/// </summary>
internal static class WindowTitle
{
    public static void Set(GameWindow window, string title)
    {
        window.Title = title;
        try
        {
            SDL_SetWindowTitle(window.Handle, Encoding.UTF8.GetBytes(title + '\0'));
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // Другая платформа (не SDL) - оставляем заголовок, выставленный MonoGame.
        }
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowTitle(IntPtr window, byte[] title);
}
