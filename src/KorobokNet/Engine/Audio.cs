using System.Diagnostics;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace KorobokNet.Engine;

public enum Sfx
{
    Click,
    Win,
}

/// <summary>
/// Музыка и звуки. Если звуковое устройство недоступно, игра продолжает работать молча.
/// </summary>
public sealed class Audio : IDisposable
{
    private const float MusicVolume = 0.35f;

    private readonly Dictionary<Sfx, SoundEffect> _effects = [];
    private readonly SoundEffect? _motorSound;
    private readonly SoundEffectInstance? _motor;
    private readonly Song? _music;
    private bool _muted;
    private float _musicPauseLeft;

    public Audio()
    {
        try
        {
            _effects[Sfx.Click] = Load("click.wav");
            _effects[Sfx.Win] = Load("end.wav");
            _motorSound = Load("forward.wav");
            _motor = _motorSound.CreateInstance();
            _motor.IsLooped = true;
            _motor.Volume = 0.6f;
            // MonoGame открывает файл песни относительно текущей папки процесса, а не папки игры.
            _music = Song.FromUri("music", new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "music.ogg"), UriKind.RelativeOrAbsolute));
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = MusicVolume;
        }
        catch (Exception e) when (e is NoAudioHardwareException or InvalidOperationException or IOException or ArgumentException)
        {
            Debug.WriteLine($"Звук недоступен: {e.Message}");
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            MediaPlayer.IsMuted = value;
            SoundEffect.MasterVolume = value ? 0f : 1f;
        }
    }

    public void StartMusic()
    {
        if (_music is not null && MediaPlayer.State != MediaState.Playing)
        {
            MediaPlayer.Play(_music);
        }
    }

    public void Play(Sfx sfx)
    {
        if (_effects.TryGetValue(sfx, out var effect))
        {
            effect.Play();
        }
    }

    public void SetMotor(bool on)
    {
        if (_motor is null)
        {
            return;
        }
        if (on && _motor.State != SoundState.Playing)
        {
            _motor.Play();
        }
        else if (!on && _motor.State == SoundState.Playing)
        {
            _motor.Stop();
        }
    }

    /// <summary>Приглушает музыку, пока играет победная мелодия.</summary>
    public void PauseMusic(float seconds)
    {
        if (MediaPlayer.State == MediaState.Playing)
        {
            MediaPlayer.Pause();
        }
        _musicPauseLeft = seconds;
    }

    public void Update(float dt)
    {
        if (_musicPauseLeft > 0)
        {
            _musicPauseLeft -= dt;
            if (_musicPauseLeft <= 0 && MediaPlayer.State == MediaState.Paused)
            {
                MediaPlayer.Resume();
            }
        }
    }

    public void Dispose()
    {
        _motor?.Dispose();
        _motorSound?.Dispose();
        foreach (var effect in _effects.Values)
        {
            effect.Dispose();
        }
        _music?.Dispose();
    }

    private static SoundEffect Load(string file)
    {
        using var stream = GameAssets.Open(Path.Combine("Audio", file));
        return SoundEffect.FromStream(stream);
    }
}
