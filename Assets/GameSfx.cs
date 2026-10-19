using System.Collections.Generic;
using UnityEngine;

/// <summary>Efectos de sonido del juego.</summary>
public enum Sfx { Hit, CityHit, Capture, Built, UnitReady, Wonder, Tech, Found, War, Peace, Alert, Select, Order, Buy, Grow, Death, Work, Research }

/// <summary>
/// Efectos de sonido sintetizados por código (sin archivos de audio): golpes de combate, ciudad capturada, construcción
/// terminada, maravilla, tecnología descubierta, ciudad fundada, guerra, paz y alertas. Cada sonido se genera la primera vez
/// que se pide y se reproduce en 2D. Muted los silencia (el juego lo cambia con F2).
/// </summary>
public static class GameSfx
{
    public static bool Muted;

    const int Rate = 22050;
    const float MasterVolume = 0.55f;

    static AudioSource source;
    static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    static readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();

    // Segundos mínimos entre dos sonidos iguales (para que una batalla grande no sature)
    static float MinGap(Sfx kind)
    {
        switch (kind)
        {
            case Sfx.Hit:     return 0.12f;
            case Sfx.CityHit: return 0.2f;
            case Sfx.Select:  return 0.05f;
            case Sfx.Order:   return 0.08f;
            case Sfx.Work:    return 0.15f;
            case Sfx.Death:   return 0.1f;
            default:          return 0.25f;
        }
    }

    public static void Play(Sfx kind, float volume = 1f)
    {
        if (Muted || volume <= 0f) return;
        if (!Ensure()) return;

        float now = Time.unscaledTime;
        float last;
        if (lastPlayed.TryGetValue(kind, out last) && now - last < MinGap(kind)) return;
        lastPlayed[kind] = now;

        source.PlayOneShot(ClipFor(kind), Mathf.Clamp01(volume) * MasterVolume);
    }

    static bool Ensure()
    {
        if (source != null) return true;

        GameObject go = new GameObject("Efectos de sonido");
        Object.DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D

        // Sin un AudioListener en la escena no se oye nada
        if (Object.FindFirstObjectByType<AudioListener>() == null && Camera.main != null)
            Camera.main.gameObject.AddComponent<AudioListener>();

        return true;
    }

    static AudioClip ClipFor(Sfx kind)
    {
        AudioClip clip;
        if (clips.TryGetValue(kind, out clip) && clip != null) return clip;

        clip = Build(kind);
        clips[kind] = clip;
        return clip;
    }

    // ---------------------------------------------------------------- Síntesis

    // Sumas de tonos y ruido sobre un búfer de muestras mono
    class Buf
    {
        public float[] d;
        public Buf(float seconds) { d = new float[Mathf.CeilToInt(seconds * Rate)]; }

        // Tono con ataque/caída exponencial; bell añade armónicos (campana), square un timbre de metal/trompeta
        public void Tone(float start, float dur, float freq, float amp, float decay = 6f, bool bell = false, bool brass = false, float attack = 0.005f, float slideTo = 0f)
        {
            int i0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            float phase = 0f;

            for (int i = 0; i < n && i0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = slideTo > 0f ? Mathf.Lerp(freq, slideTo, t / dur) : freq;
                phase += 2f * Mathf.PI * f / Rate;

                float env = Mathf.Min(1f, t / attack) * Mathf.Exp(-decay * t);
                float s = Mathf.Sin(phase);
                if (bell) s += 0.45f * Mathf.Sin(phase * 2.01f) + 0.2f * Mathf.Sin(phase * 3.02f);
                if (brass) s += 0.5f * Mathf.Sin(phase * 2f) + 0.33f * Mathf.Sin(phase * 3f) + 0.2f * Mathf.Sin(phase * 5f);

                d[i0 + i] += s * env * amp;
            }
        }

        // Ráfaga de ruido filtrado (golpes, explosiones); cutoff 0..1 (más bajo = más grave)
        public void Noise(float start, float dur, float amp, float decay = 25f, float cutoff = 0.5f)
        {
            int i0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(dur * Rate);
            float lp = 0f;

            for (int i = 0; i < n && i0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Random.value * 2f - 1f - lp) * cutoff;
                d[i0 + i] += lp * Mathf.Exp(-decay * t) * amp;
            }
        }

        public AudioClip ToClip(string name)
        {
            // Limitador suave para que nunca sature
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i]);

            AudioClip clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }

    static AudioClip Build(Sfx kind)
    {
        switch (kind)
        {
            case Sfx.Hit:
            {
                Buf b = new Buf(0.22f);
                b.Noise(0f, 0.2f, 0.9f, 30f, 0.55f);
                b.Tone(0f, 0.15f, 150f, 0.8f, 22f, false, false, 0.002f, 70f);
                return b.ToClip("sfx-golpe");
            }
            case Sfx.CityHit:
            {
                Buf b = new Buf(0.4f);
                b.Noise(0f, 0.35f, 1f, 12f, 0.25f);
                b.Tone(0f, 0.3f, 90f, 0.9f, 10f, false, false, 0.002f, 45f);
                b.Tone(0.04f, 0.2f, 160f, 0.4f, 14f);
                return b.ToClip("sfx-ciudad-golpeada");
            }
            case Sfx.Capture:
            {
                Buf b = new Buf(1f);
                b.Noise(0f, 0.25f, 0.8f, 10f, 0.3f);
                b.Tone(0.05f, 0.25f, 196f, 0.5f, 4f, false, true, 0.02f);
                b.Tone(0.25f, 0.25f, 247f, 0.5f, 4f, false, true, 0.02f);
                b.Tone(0.45f, 0.5f, 330f, 0.55f, 3f, false, true, 0.02f);
                return b.ToClip("sfx-ciudad-capturada");
            }
            case Sfx.Built:
            {
                Buf b = new Buf(0.8f);
                b.Tone(0f, 0.6f, 523.25f, 0.5f, 6f, true);
                b.Tone(0.14f, 0.6f, 783.99f, 0.5f, 5f, true);
                return b.ToClip("sfx-construido");
            }
            case Sfx.UnitReady:
            {
                Buf b = new Buf(0.35f);
                b.Tone(0f, 0.15f, 659.25f, 0.5f, 12f, true);
                b.Tone(0.1f, 0.2f, 880f, 0.5f, 10f, true);
                return b.ToClip("sfx-unidad-lista");
            }
            case Sfx.Wonder:
            {
                Buf b = new Buf(1.6f);
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                for (int i = 0; i < notes.Length; i++) b.Tone(i * 0.15f, 0.4f, notes[i], 0.4f, 4f, false, true, 0.01f);
                b.Tone(0.6f, 0.9f, 523.25f, 0.3f, 2.5f, true);
                b.Tone(0.6f, 0.9f, 783.99f, 0.3f, 2.5f, true);
                b.Tone(0.6f, 0.9f, 1046.5f, 0.3f, 2.5f, true);
                return b.ToClip("sfx-maravilla");
            }
            case Sfx.Tech:
            {
                Buf b = new Buf(0.9f);
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
                for (int i = 0; i < notes.Length; i++) b.Tone(i * 0.08f, 0.5f, notes[i], 0.4f, 6f, true);
                return b.ToClip("sfx-tecnologia");
            }
            case Sfx.Found:
            {
                Buf b = new Buf(0.9f);
                b.Tone(0f, 0.8f, 196f, 0.45f, 2.5f, false, true, 0.08f);
                b.Tone(0f, 0.8f, 293.66f, 0.35f, 2.5f, false, true, 0.08f);
                return b.ToClip("sfx-ciudad-fundada");
            }
            case Sfx.War:
            {
                Buf b = new Buf(1f);
                b.Tone(0f, 0.9f, 220f, 0.5f, 2.5f, false, true, 0.04f, 150f);
                b.Tone(0f, 0.2f, 70f, 0.9f, 8f, false, false, 0.002f, 40f);
                b.Tone(0.3f, 0.2f, 70f, 0.9f, 8f, false, false, 0.002f, 40f);
                b.Noise(0f, 0.2f, 0.3f, 12f, 0.2f);
                return b.ToClip("sfx-guerra");
            }
            case Sfx.Peace:
            {
                Buf b = new Buf(0.9f);
                b.Tone(0f, 0.6f, 659.25f, 0.45f, 5f, true);
                b.Tone(0.22f, 0.6f, 523.25f, 0.45f, 5f, true);
                return b.ToClip("sfx-paz");
            }
            case Sfx.Select:
            {
                Buf b = new Buf(0.08f);
                b.Tone(0f, 0.06f, 1200f, 0.45f, 60f);
                b.Tone(0f, 0.06f, 1800f, 0.25f, 70f);
                return b.ToClip("sfx-seleccion");
            }
            case Sfx.Order:
            {
                Buf b = new Buf(0.14f);
                b.Tone(0f, 0.12f, 660f, 0.45f, 22f, false, false, 0.002f, 880f);
                return b.ToClip("sfx-orden");
            }
            case Sfx.Buy:
            {
                Buf b = new Buf(0.5f);
                b.Tone(0f, 0.3f, 1318.5f, 0.45f, 9f, true);
                b.Tone(0.07f, 0.4f, 1760f, 0.45f, 8f, true);
                return b.ToClip("sfx-compra");
            }
            case Sfx.Grow:
            {
                Buf b = new Buf(0.5f);
                b.Tone(0f, 0.3f, 440f, 0.35f, 7f, true);
                b.Tone(0.12f, 0.35f, 554.37f, 0.35f, 6f, true);
                return b.ToClip("sfx-crecimiento");
            }
            case Sfx.Death:
            {
                Buf b = new Buf(0.4f);
                b.Noise(0f, 0.3f, 0.6f, 14f, 0.2f);
                b.Tone(0f, 0.35f, 130f, 0.7f, 9f, false, false, 0.003f, 55f);
                return b.ToClip("sfx-muerte");
            }
            case Sfx.Work:
            {
                Buf b = new Buf(0.14f);
                b.Noise(0f, 0.05f, 0.6f, 60f, 0.7f);
                b.Tone(0f, 0.1f, 320f, 0.6f, 45f, false, false, 0.001f, 200f);
                return b.ToClip("sfx-obra");
            }
            case Sfx.Research:
            {
                Buf b = new Buf(0.35f);
                b.Tone(0f, 0.3f, 440f, 0.35f, 8f, true, false, 0.01f, 660f);
                return b.ToClip("sfx-investigar");
            }
            default:   // Alert
            {
                Buf b = new Buf(0.5f);
                b.Tone(0f, 0.14f, 880f, 0.5f, 14f, false, true, 0.003f);
                b.Tone(0.2f, 0.14f, 880f, 0.5f, 14f, false, true, 0.003f);
                return b.ToClip("sfx-alerta");
            }
        }
    }
}
