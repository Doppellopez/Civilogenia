using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// Activa o desactiva el soporte de Unity Relay (símbolo CIV_RELAY) según estén o no instalados los paquetes
/// com.unity.services.relay y com.unity.transport. Así el juego compila igual sin ellos.
/// </summary>
[InitializeOnLoad]
public static class RelaySetup
{
    const string Define = "CIV_RELAY";

    static RelaySetup()
    {
        // El SDK de Relay viene en el paquete com.unity.services.relay (assembly Unity.Services.Relay) o incluido en
        // com.unity.services.multiplayer (assembly Unity.Services.Multiplayer), con la misma API
        bool installed = (HasAssembly("Unity.Services.Relay") || HasAssembly("Unity.Services.Multiplayer")) && HasAssembly("Unity.Networking.Transport");

        foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Server })
        {
            List<string> defines = new List<string>(PlayerSettings.GetScriptingDefineSymbols(target).Split(';', StringSplitOptions.RemoveEmptyEntries));
            bool has = defines.Contains(Define);
            if (installed == has) continue;

            if (installed) defines.Add(Define); else defines.Remove(Define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            UnityEngine.Debug.Log("[Civilogenia] Relay " + (installed ? "activado" : "desactivado") + " para " + target.TargetName + ".");
        }
    }

    static bool HasAssembly(string name)
    {
        foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            if (a.GetName().Name == name) return true;
        return false;
    }
}
