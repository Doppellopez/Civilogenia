using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Entrada de teclado y ratón con el paquete Input System. Mantiene la forma de la clase Input antigua
/// (GetKey, GetKeyDown, GetMouseButtonDown, mousePosition, mouseScrollDelta) para que el resto del código no cambie.
/// </summary>
public static class Inp
{
    // Tecla de Input System equivalente a un KeyCode (solo las que usa el juego); Key.None si no hay equivalente
    static Key ToKey(KeyCode code)
    {
        switch (code)
        {
            case KeyCode.A: return Key.A;
            case KeyCode.B: return Key.B;
            case KeyCode.C: return Key.C;
            case KeyCode.D: return Key.D;
            case KeyCode.F: return Key.F;
            case KeyCode.H: return Key.H;
            case KeyCode.I: return Key.I;
            case KeyCode.J: return Key.J;
            case KeyCode.K: return Key.K;
            case KeyCode.M: return Key.M;
            case KeyCode.N: return Key.N;
            case KeyCode.R: return Key.R;
            case KeyCode.T: return Key.T;
            case KeyCode.U: return Key.U;
            case KeyCode.V: return Key.V;
            case KeyCode.F1: return Key.F1;
            case KeyCode.F2: return Key.F2;
            case KeyCode.F5: return Key.F5;
            case KeyCode.F9: return Key.F9;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.KeypadEnter: return Key.NumpadEnter;
            case KeyCode.Space: return Key.Space;
            case KeyCode.Backspace: return Key.Backspace;
            case KeyCode.LeftArrow: return Key.LeftArrow;
            case KeyCode.RightArrow: return Key.RightArrow;
            case KeyCode.UpArrow: return Key.UpArrow;
            case KeyCode.DownArrow: return Key.DownArrow;
            case KeyCode.LeftShift: return Key.LeftShift;
            case KeyCode.RightShift: return Key.RightShift;
            case KeyCode.LeftControl: return Key.LeftCtrl;
            case KeyCode.RightControl: return Key.RightCtrl;
            case KeyCode.LeftAlt: return Key.LeftAlt;
            case KeyCode.RightAlt: return Key.RightAlt;
            case KeyCode.Comma: return Key.Comma;
            case KeyCode.Period: return Key.Period;
            case KeyCode.Slash: return Key.Slash;
            case KeyCode.Minus: return Key.Minus;
            case KeyCode.KeypadMinus: return Key.NumpadMinus;
            case KeyCode.KeypadPlus: return Key.NumpadPlus;
            case KeyCode.Plus:
            case KeyCode.Equals: return Key.Equals;
            default: return Key.None;
        }
    }

    public static bool GetKey(KeyCode code)
    {
        Key key = ToKey(code);
        return key != Key.None && Keyboard.current != null && Keyboard.current[key].isPressed;
    }

    public static bool GetKeyDown(KeyCode code)
    {
        Key key = ToKey(code);
        return key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }

    public static bool GetMouseButtonDown(int button)
    {
        Mouse m = Mouse.current;
        if (m == null) return false;
        switch (button)
        {
            case 0: return m.leftButton.wasPressedThisFrame;
            case 1: return m.rightButton.wasPressedThisFrame;
            case 2: return m.middleButton.wasPressedThisFrame;
            default: return false;
        }
    }

    public static Vector3 mousePosition
    {
        get
        {
            Mouse m = Mouse.current;
            if (m == null) return Vector3.zero;
            Vector2 p = m.position.ReadValue();
            return new Vector3(p.x, p.y, 0f);
        }
    }

    // La rueda del Input System da 120 por muesca en Windows; la clase Input antigua daba 1
    public static Vector2 mouseScrollDelta
    {
        get
        {
            Mouse m = Mouse.current;
            if (m == null) return Vector2.zero;
            Vector2 s = m.scroll.ReadValue();
            return Mathf.Abs(s.y) >= 10f || Mathf.Abs(s.x) >= 10f ? s / 120f : s;
        }
    }
}
